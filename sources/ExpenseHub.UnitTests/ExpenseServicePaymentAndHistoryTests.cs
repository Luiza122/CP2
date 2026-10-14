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
internal sealed class ExpenseServicePaymentAndHistoryTests
{
    private static readonly DateTimeOffset _now =
        new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    internal async Task PayAsync_RecordsPaymentActorTimeAndHistoryTogether()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<ExpenseHubDbContext> options = new DbContextOptionsBuilder<ExpenseHubDbContext>()
            .UseSqlite(connection)
            .Options;
        await using ExpenseHubDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();

        Expense expense = AddApprovedExpense(context, "employee-1");
        await context.SaveChangesAsync();
        ExpenseService service = new(context, new FixedTimeProvider(_now));

        var paid = await service.PayAsync(
            expense.Id, "finance-1", [Roles.Finance], CancellationToken.None);

        Assert.AreEqual(ExpenseStatus.Paid, paid.Status);
        Assert.AreEqual(_now, paid.UpdatedAtUtc);

        PaymentRecord payment = await context.PaymentRecords.SingleAsync();
        Assert.AreEqual(expense.Id, payment.ExpenseId);
        Assert.AreEqual("finance-1", payment.ActorUserId);
        Assert.AreEqual(_now, payment.PaidAtUtc);

        ExpenseHistory history = await context.ExpenseHistories.SingleAsync(item => item.Action == "Paid");
        Assert.AreEqual("finance-1", history.ActorUserId);
        Assert.AreEqual(ExpenseStatus.Approved, history.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Paid, history.NewStatus);
        Assert.AreEqual(_now, history.OccurredAtUtc);
    }

    [TestMethod]
    internal async Task PayAsync_RejectsInvalidStateAndDuplicatePayment()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<ExpenseHubDbContext> options = new DbContextOptionsBuilder<ExpenseHubDbContext>()
            .UseSqlite(connection)
            .Options;
        await using ExpenseHubDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();

        Expense submittedExpense = AddExpense(context, "employee-1", ExpenseStatus.Submitted);
        Expense approvedExpense = AddApprovedExpense(context, "employee-2");
        await context.SaveChangesAsync();
        ExpenseService service = new(context, new FixedTimeProvider(_now));

        await Assert.ThrowsExactlyAsync<ConflictException>(() => service.PayAsync(
            submittedExpense.Id, "finance-1", [Roles.Finance], CancellationToken.None));
        await service.PayAsync(approvedExpense.Id, "finance-1", [Roles.Finance], CancellationToken.None);
        await Assert.ThrowsExactlyAsync<ConflictException>(() => service.PayAsync(
            approvedExpense.Id, "finance-2", [Roles.Finance], CancellationToken.None));

        Assert.AreEqual(1, await context.PaymentRecords.CountAsync());
        Assert.AreEqual(1, await context.ExpenseHistories.CountAsync(item => item.Action == "Paid"));
        Assert.AreEqual(ExpenseStatus.Submitted, await ReadStatusAsync(context, submittedExpense.Id));
        Assert.AreEqual(ExpenseStatus.Paid, await ReadStatusAsync(context, approvedExpense.Id));
    }

    [TestMethod]
    internal async Task PayAsync_PersistenceFailureDoesNotPartiallyChangeStatusOrHistory()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<ExpenseHubDbContext> options = new DbContextOptionsBuilder<ExpenseHubDbContext>()
            .UseSqlite(connection)
            .Options;
        await using ExpenseHubDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();

        Expense expense = AddApprovedExpense(context, "employee-1");
        context.PaymentRecords.Add(new PaymentRecord
        {
            Id = Guid.NewGuid(),
            ExpenseId = expense.Id,
            ActorUserId = "finance-existing",
            PaidAtUtc = _now.AddMinutes(-1),
        });
        await context.SaveChangesAsync();
        ExpenseService service = new(context, new FixedTimeProvider(_now));

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => service.PayAsync(
            expense.Id, "finance-1", [Roles.Finance], CancellationToken.None));

        await using ExpenseHubDbContext verificationContext = new(options);
        Assert.AreEqual(ExpenseStatus.Approved, await ReadStatusAsync(verificationContext, expense.Id));
        Assert.AreEqual(1, await verificationContext.PaymentRecords.CountAsync());
        Assert.AreEqual(0, await verificationContext.ExpenseHistories.CountAsync(item => item.Action == "Paid"));
    }

    [TestMethod]
    internal async Task GetHistoryAsync_ReturnsOrderedHistoryForVisibleExpense()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<ExpenseHubDbContext> options = new DbContextOptionsBuilder<ExpenseHubDbContext>()
            .UseSqlite(connection)
            .Options;
        await using ExpenseHubDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();

        Expense expense = AddApprovedExpense(context, "employee-1");
        await context.SaveChangesAsync();
        ExpenseService service = new(context, new FixedTimeProvider(_now));

        var history = await service.GetHistoryAsync(
            expense.Id, "auditor-1", [Roles.Auditor], CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "Created", "Submitted", "Approved" },
            history.Select(item => item.Action).ToArray());
    }

    private static Expense AddApprovedExpense(ExpenseHubDbContext context, string ownerId)
    {
        Expense expense = AddExpense(context, ownerId, ExpenseStatus.Draft);
        context.ExpenseHistories.Add(ExpenseHistory.Create(expense, "Created", ownerId, null, null, null, _now));

        ExpenseStatus previousStatus = expense.Status;
        expense.Status = ExpenseStatus.Submitted;
        context.ExpenseHistories.Add(
            ExpenseHistory.Create(expense, "Submitted", ownerId, previousStatus, null, null, _now.AddMinutes(1)));

        previousStatus = expense.Status;
        expense.Status = ExpenseStatus.Approved;
        context.ExpenseHistories.Add(
            ExpenseHistory.Create(expense, "Approved", "approver-1", previousStatus, null, null, _now.AddMinutes(2)));
        return expense;
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

    private static async Task<ExpenseStatus> ReadStatusAsync(ExpenseHubDbContext context, Guid expenseId)
    {
        return await context.Expenses
            .AsNoTracking()
            .Where(item => item.Id == expenseId)
            .Select(item => item.Status)
            .SingleAsync();
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
