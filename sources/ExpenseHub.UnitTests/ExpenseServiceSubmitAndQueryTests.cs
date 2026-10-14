using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Infrastructure;
using ExpenseHub.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class ExpenseServiceSubmitAndQueryTests
{
    private static readonly DateTimeOffset _now =
        new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    internal async Task SubmitAsync_OwnerMovesDraftToSubmittedAndRecordsHistory()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<ExpenseHubDbContext> options = new DbContextOptionsBuilder<ExpenseHubDbContext>()
            .UseSqlite(connection)
            .Options;
        await using ExpenseHubDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();

        Expense expense = AddExpense(context, "employee-1", ExpenseStatus.Draft);
        await context.SaveChangesAsync();
        ExpenseService service = new(context, new FixedTimeProvider(_now));

        var response = await service.SubmitAsync(expense.Id, "employee-1", CancellationToken.None);

        Assert.AreEqual(ExpenseStatus.Submitted, response.Status);
        Assert.AreEqual(_now, response.UpdatedAtUtc);
        ExpenseHistory history = await context.ExpenseHistories.SingleAsync(item => item.Action == "Submitted");
        Assert.AreEqual("employee-1", history.ActorUserId);
        Assert.AreEqual(ExpenseStatus.Draft, history.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Submitted, history.NewStatus);
        Assert.AreEqual(_now, history.OccurredAtUtc);
    }

    [TestMethod]
    internal async Task SubmitAsync_NonOwnerCannotSubmitDraft()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<ExpenseHubDbContext> options = new DbContextOptionsBuilder<ExpenseHubDbContext>()
            .UseSqlite(connection)
            .Options;
        await using ExpenseHubDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();

        Expense expense = AddExpense(context, "employee-1", ExpenseStatus.Draft);
        await context.SaveChangesAsync();
        ExpenseService service = new(context, new FixedTimeProvider(_now));

        await Assert.ThrowsExactlyAsync<ForbiddenException>(
            () => service.SubmitAsync(expense.Id, "employee-2", CancellationToken.None));

        Assert.AreEqual(ExpenseStatus.Draft, await context.Expenses
            .Where(item => item.Id == expense.Id)
            .Select(item => item.Status)
            .SingleAsync());
        Assert.AreEqual(0, await context.ExpenseHistories.CountAsync(item => item.Action == "Submitted"));
    }

    [TestMethod]
    internal async Task SubmitAsync_CannotSubmitSameExpenseTwice()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<ExpenseHubDbContext> options = new DbContextOptionsBuilder<ExpenseHubDbContext>()
            .UseSqlite(connection)
            .Options;
        await using ExpenseHubDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();

        Expense expense = AddExpense(context, "employee-1", ExpenseStatus.Draft);
        await context.SaveChangesAsync();
        ExpenseService service = new(context, new FixedTimeProvider(_now));

        await service.SubmitAsync(expense.Id, "employee-1", CancellationToken.None);
        await Assert.ThrowsExactlyAsync<ConflictException>(
            () => service.SubmitAsync(expense.Id, "employee-1", CancellationToken.None));

        Assert.AreEqual(1, await context.ExpenseHistories.CountAsync(item => item.Action == "Submitted"));
    }

    [TestMethod]
    internal async Task ListAsync_FiltersByProfileBeforeReturningResults()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<ExpenseHubDbContext> options = new DbContextOptionsBuilder<ExpenseHubDbContext>()
            .UseSqlite(connection)
            .Options;
        await using ExpenseHubDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();

        Expense ownDraft = AddExpense(context, "employee-1", ExpenseStatus.Draft);
        AddExpense(context, "employee-2", ExpenseStatus.Draft);
        Expense submitted = AddExpense(context, "employee-3", ExpenseStatus.Submitted);
        Expense approved = AddExpense(context, "employee-4", ExpenseStatus.Approved);
        Expense paid = AddExpense(context, "employee-5", ExpenseStatus.Paid);
        Expense rejected = AddExpense(context, "employee-6", ExpenseStatus.Rejected);
        await context.SaveChangesAsync();
        ExpenseService service = new(context, new FixedTimeProvider(_now));

        var employeeItems = await service.ListAsync("employee-1", [Roles.Employee], CancellationToken.None);
        var approverItems = await service.ListAsync("approver-1", [Roles.Approver], CancellationToken.None);
        var financeItems = await service.ListAsync("finance-1", [Roles.Finance], CancellationToken.None);
        var auditorItems = await service.ListAsync("auditor-1", [Roles.Auditor], CancellationToken.None);
        var adminItems = await service.ListAsync("admin-1", [Roles.Admin], CancellationToken.None);

        CollectionAssert.AreEqual(new[] { ownDraft.Id }, employeeItems.Select(item => item.Id).ToArray());
        CollectionAssert.AreEqual(new[] { submitted.Id }, approverItems.Select(item => item.Id).ToArray());
        CollectionAssert.AreEquivalent(new[] { approved.Id, paid.Id }, financeItems.Select(item => item.Id).ToArray());
        Assert.AreEqual(6, auditorItems.Count);
        Assert.AreEqual(0, adminItems.Count);
        Assert.IsFalse(employeeItems.Any(item => item.Id == rejected.Id));
    }

    [TestMethod]
    internal async Task GetAsync_HidesExpensesOutsideUserScopeAsNotFound()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<ExpenseHubDbContext> options = new DbContextOptionsBuilder<ExpenseHubDbContext>()
            .UseSqlite(connection)
            .Options;
        await using ExpenseHubDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();

        Expense expense = AddExpense(context, "employee-1", ExpenseStatus.Draft);
        await context.SaveChangesAsync();
        ExpenseService service = new(context, new FixedTimeProvider(_now));

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => service.GetAsync(expense.Id, "employee-2", [Roles.Employee], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => service.GetAsync(Guid.NewGuid(), "auditor-1", [Roles.Auditor], CancellationToken.None));
    }

    private static Expense AddExpense(ExpenseHubDbContext context, string ownerId, ExpenseStatus status)
    {
        Expense expense = Expense.Create(
            ownerId,
            "Business expense for project travel",
            125.50m,
            new DateOnly(2026, 9, 30),
            null,
            _now);
        expense.Status = status;
        context.Expenses.Add(expense);
        return expense;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }
}
