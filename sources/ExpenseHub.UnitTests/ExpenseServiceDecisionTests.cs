using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Infrastructure;
using ExpenseHub.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class ExpenseServiceDecisionTests
{
    private static readonly DateTimeOffset _now =
        new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    internal async Task ApproveAsync_RequiresApproverAndRecordsOneDecision()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<ExpenseHubDbContext> options = new DbContextOptionsBuilder<ExpenseHubDbContext>()
            .UseSqlite(connection)
            .Options;
        await using ExpenseHubDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();

        Expense expense = AddExpense(context, "employee-1", ExpenseStatus.Submitted);
        await context.SaveChangesAsync();
        ExpenseService service = new(context, new FixedTimeProvider(_now));

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => service.ApproveAsync(
            expense.Id, "employee-2", [Roles.Employee], CancellationToken.None));

        var approved = await service.ApproveAsync(
            expense.Id, "approver-1", [Roles.Approver], CancellationToken.None);

        Assert.AreEqual(ExpenseStatus.Approved, approved.Status);
        Assert.AreEqual(_now, approved.UpdatedAtUtc);
        await Assert.ThrowsExactlyAsync<ConflictException>(() => service.ApproveAsync(
            expense.Id, "approver-2", [Roles.Approver], CancellationToken.None));

        ExpenseHistory history = await context.ExpenseHistories.SingleAsync(item => item.Action == "Approved");
        Assert.AreEqual("approver-1", history.ActorUserId);
        Assert.AreEqual(ExpenseStatus.Submitted, history.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Approved, history.NewStatus);
        Assert.AreEqual(_now, history.OccurredAtUtc);
        Assert.AreEqual(1, await context.ExpenseHistories.CountAsync(item => item.Action == "Approved"));
    }

    [TestMethod]
    internal async Task RejectAsync_ValidatesJustificationBoundariesAndPersistsActor()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<ExpenseHubDbContext> options = new DbContextOptionsBuilder<ExpenseHubDbContext>()
            .UseSqlite(connection)
            .Options;
        await using ExpenseHubDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();

        Expense expense = AddExpense(context, "employee-1", ExpenseStatus.Submitted);
        await context.SaveChangesAsync();
        ExpenseService service = new(context, new FixedTimeProvider(_now));

        await Assert.ThrowsExactlyAsync<ValidationException>(() => service.RejectAsync(
            expense.Id,
            "approver-1",
            [Roles.Approver],
            new RejectExpenseRequest("123456789"),
            CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ValidationException>(() => service.RejectAsync(
            expense.Id,
            "approver-1",
            [Roles.Approver],
            new RejectExpenseRequest(new string('x', 501)),
            CancellationToken.None));

        var rejected = await service.RejectAsync(
            expense.Id,
            "approver-1",
            [Roles.Approver],
            new RejectExpenseRequest("1234567890"),
            CancellationToken.None);

        Assert.AreEqual(ExpenseStatus.Rejected, rejected.Status);
        ExpenseHistory history = await context.ExpenseHistories.SingleAsync(item => item.Action == "Rejected");
        Assert.AreEqual("approver-1", history.ActorUserId);
        Assert.AreEqual("1234567890", history.Justification);
        Assert.AreEqual(ExpenseStatus.Submitted, history.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Rejected, history.NewStatus);
        Assert.AreEqual(1, await context.ExpenseHistories.CountAsync(item => item.Action == "Rejected"));
    }

    [TestMethod]
    internal async Task RejectAsync_OwnerCannotRejectOwnExpense()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<ExpenseHubDbContext> options = new DbContextOptionsBuilder<ExpenseHubDbContext>()
            .UseSqlite(connection)
            .Options;
        await using ExpenseHubDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();

        Expense expense = AddExpense(context, "employee-1", ExpenseStatus.Submitted);
        await context.SaveChangesAsync();
        ExpenseService service = new(context, new FixedTimeProvider(_now));

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => service.RejectAsync(
            expense.Id,
            "employee-1",
            [Roles.Employee, Roles.Approver],
            new RejectExpenseRequest("Valid rejection reason"),
            CancellationToken.None));

        Assert.AreEqual(ExpenseStatus.Submitted, await context.Expenses
            .Where(item => item.Id == expense.Id)
            .Select(item => item.Status)
            .SingleAsync());
        Assert.AreEqual(0, await context.ExpenseHistories.CountAsync(item => item.Action == "Rejected"));
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
        context.ExpenseHistories.Add(
            ExpenseHistory.Create(expense, "Created", ownerId, null, null, null, _now));
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
