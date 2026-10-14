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
internal sealed class ExpenseServiceAuthorizationTests
{
    private static readonly DateTimeOffset _now =
        new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    internal async Task ApproverWithEmployeeRole_CannotApproveOwnExpense()
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
            expense.Id,
            "employee-1",
            [Roles.Employee, Roles.Approver],
            CancellationToken.None));

        Assert.AreEqual(ExpenseStatus.Submitted, await ReadStatusAsync(context, expense.Id));
        Assert.AreEqual(0, await context.ExpenseHistories.CountAsync(item => item.Action == "Approved"));
    }

    [TestMethod]
    internal async Task FinanceWithEmployeeRole_CannotPayOwnExpense()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<ExpenseHubDbContext> options = new DbContextOptionsBuilder<ExpenseHubDbContext>()
            .UseSqlite(connection)
            .Options;
        await using ExpenseHubDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();

        Expense expense = AddExpense(context, "employee-1", ExpenseStatus.Approved);
        await context.SaveChangesAsync();
        ExpenseService service = new(context, new FixedTimeProvider(_now));

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => service.PayAsync(
            expense.Id,
            "employee-1",
            [Roles.Employee, Roles.Finance],
            CancellationToken.None));

        Assert.AreEqual(ExpenseStatus.Approved, await ReadStatusAsync(context, expense.Id));
        Assert.AreEqual(0, await context.PaymentRecords.CountAsync());
        Assert.AreEqual(0, await context.ExpenseHistories.CountAsync(item => item.Action == "Paid"));
    }

    [TestMethod]
    internal async Task AuditorAndAdminWithoutFunctionalRole_CannotPerformDecisions()
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
            expense.Id, "auditor-1", [Roles.Auditor], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => service.ApproveAsync(
            expense.Id, "admin-1", [Roles.Admin], CancellationToken.None));

        Assert.AreEqual(ExpenseStatus.Submitted, await ReadStatusAsync(context, expense.Id));
        Assert.AreEqual(0, await context.ExpenseHistories.CountAsync(item => item.Action == "Approved"));
    }

    [TestMethod]
    internal async Task GetHistoryAsync_UsesTheSameVisibilityAsExpenseDetail()
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

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => service.GetHistoryAsync(
            expense.Id, "employee-2", [Roles.Employee], CancellationToken.None));

        var history = await service.GetHistoryAsync(
            expense.Id, "auditor-1", [Roles.Auditor], CancellationToken.None);
        Assert.AreEqual(1, history.Count);
        Assert.AreEqual("Created", history.Single().Action);
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
