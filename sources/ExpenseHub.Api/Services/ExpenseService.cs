using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Services;

public sealed class ExpenseService
{
    private readonly ExpenseHubDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public ExpenseService(ExpenseHubDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<ExpenseResponse> CreateAsync(
        string userId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);

        Expense expense = Expense.Create(
            userId,
            request.Description ?? string.Empty,
            request.Amount,
            request.ExpenseDate,
            request.CategoryId,
            now);

        _dbContext.Expenses.Add(expense);
        _dbContext.ExpenseHistories.Add(ExpenseHistory.Create(expense, "Created", userId, null, null, null, now));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(expense);
    }

    public async Task<ExpenseResponse> UpdateAsync(
        Guid id,
        string userId,
        UpdateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        Expense expense = await FindForWriteAsync(id, cancellationToken);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);

        string changes = expense.Edit(
            userId,
            request.Description ?? string.Empty,
            request.Amount,
            request.ExpenseDate,
            request.CategoryId,
            now);

        _dbContext.ExpenseHistories.Add(
            ExpenseHistory.Create(expense, "Updated", userId, ExpenseStatus.Draft, null, changes, now));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(expense);
    }

    public async Task<ExpenseResponse> SubmitAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        Expense expense = await FindForWriteAsync(id, cancellationToken);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        ExpenseStatus previousStatus = expense.Status;
        expense.Submit(userId, now);
        _dbContext.ExpenseHistories.Add(
            ExpenseHistory.Create(expense, "Submitted", userId, previousStatus, null, null, now));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(expense);
    }

    public async Task<ExpenseResponse> ApproveAsync(
        Guid id,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        EnsureRole(roles, Roles.Approver);
        Expense expense = await FindForWriteAsync(id, cancellationToken);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        ExpenseStatus previousStatus = expense.Status;
        expense.Approve(userId, now);
        _dbContext.ExpenseHistories.Add(
            ExpenseHistory.Create(expense, "Approved", userId, previousStatus, null, null, now));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(expense);
    }

    public async Task<ExpenseResponse> RejectAsync(
        Guid id,
        string userId,
        IReadOnlyCollection<string> roles,
        RejectExpenseRequest request,
        CancellationToken cancellationToken)
    {
        EnsureRole(roles, Roles.Approver);
        Expense expense = await FindForWriteAsync(id, cancellationToken);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        ExpenseStatus previousStatus = expense.Status;
        string justification = request.Justification ?? string.Empty;
        expense.Reject(userId, justification, now);
        _dbContext.ExpenseHistories.Add(
            ExpenseHistory.Create(expense, "Rejected", userId, previousStatus, justification, null, now));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(expense);
    }

    public async Task<ExpenseResponse> PayAsync(
        Guid id,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        EnsureRole(roles, Roles.Finance);
        Expense expense = await FindForWriteAsync(id, cancellationToken);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        ExpenseStatus previousStatus = expense.Status;
        expense.Pay(userId, now);

        _dbContext.PaymentRecords.Add(new PaymentRecord
        {
            Id = Guid.NewGuid(),
            ExpenseId = expense.Id,
            ActorUserId = userId,
            PaidAtUtc = now,
        });
        _dbContext.ExpenseHistories.Add(
            ExpenseHistory.Create(expense, "Paid", userId, previousStatus, null, null, now));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(expense);
    }

    public async Task<IReadOnlyCollection<ExpenseResponse>> ListAsync(
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        IQueryable<Expense> query = ApplyVisibility(_dbContext.Expenses.AsNoTracking(), userId, roles);

        List<ExpenseResponse> expenses = await query
            .OrderByDescending(expense => expense.CreatedAtUtc)
            .Select(expense => new ExpenseResponse(
                expense.Id,
                expense.OwnerId,
                expense.Description,
                expense.Amount,
                expense.ExpenseDate,
                expense.Status,
                expense.ExpenseCategoryId,
                expense.CreatedAtUtc,
                expense.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return expenses;
    }

    public async Task<ExpenseResponse> GetAsync(
        Guid id,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        ExpenseResponse? expense = await ApplyVisibility(_dbContext.Expenses.AsNoTracking(), userId, roles)
            .Where(item => item.Id == id)
            .Select(item => new ExpenseResponse(
                item.Id,
                item.OwnerId,
                item.Description,
                item.Amount,
                item.ExpenseDate,
                item.Status,
                item.ExpenseCategoryId,
                item.CreatedAtUtc,
                item.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        return expense ?? throw new NotFoundException("Expense was not found.");
    }

    public async Task<IReadOnlyCollection<ExpenseHistoryResponse>> GetHistoryAsync(
        Guid id,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        bool visible = await ApplyVisibility(_dbContext.Expenses.AsNoTracking(), userId, roles)
            .AnyAsync(expense => expense.Id == id, cancellationToken);

        if (!visible)
        {
            throw new NotFoundException("Expense was not found.");
        }

        List<ExpenseHistoryResponse> history = await _dbContext.ExpenseHistories
            .AsNoTracking()
            .Where(item => item.ExpenseId == id)
            .OrderBy(item => item.OccurredAtUtc)
            .Select(item => new ExpenseHistoryResponse(
                item.Id,
                item.Action,
                item.ActorUserId,
                item.OccurredAtUtc,
                item.PreviousStatus,
                item.NewStatus,
                item.Justification,
                item.Changes))
            .ToListAsync(cancellationToken);

        return history;
    }

    private static IQueryable<Expense> ApplyVisibility(
        IQueryable<Expense> query,
        string userId,
        IReadOnlyCollection<string> roles)
    {
        bool isAuditor = ExpenseAccessPolicy.HasRole(roles, Roles.Auditor);
        bool isEmployee = ExpenseAccessPolicy.HasRole(roles, Roles.Employee);
        bool isApprover = ExpenseAccessPolicy.HasRole(roles, Roles.Approver);
        bool isFinance = ExpenseAccessPolicy.HasRole(roles, Roles.Finance);

        if (isAuditor)
        {
            return query;
        }

        return query.Where(expense =>
            (isEmployee && expense.OwnerId == userId)
            || (isApprover && expense.Status == ExpenseStatus.Submitted)
            || (isFinance && (expense.Status == ExpenseStatus.Approved || expense.Status == ExpenseStatus.Paid)));
    }

    private static ExpenseResponse ToResponse(Expense expense)
    {
        return new ExpenseResponse(
            expense.Id,
            expense.OwnerId,
            expense.Description,
            expense.Amount,
            expense.ExpenseDate,
            expense.Status,
            expense.ExpenseCategoryId,
            expense.CreatedAtUtc,
            expense.UpdatedAtUtc);
    }

    private static void EnsureRole(IReadOnlyCollection<string> roles, string role)
    {
        if (!ExpenseAccessPolicy.HasRole(roles, role))
        {
            throw new ForbiddenException("The authenticated user does not have the required role.");
        }
    }

    private async Task<Expense> FindForWriteAsync(Guid id, CancellationToken cancellationToken)
    {
        Expense? expense = await _dbContext.Expenses.SingleOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);

        return expense ?? throw new NotFoundException("Expense was not found.");
    }

    private async Task EnsureCategoryExistsAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId.HasValue
            && !await _dbContext.ExpenseCategories.AnyAsync(
                category => category.Id == categoryId.Value,
                cancellationToken))
        {
            throw new ValidationException("The informed expense category does not exist.");
        }
    }
}
