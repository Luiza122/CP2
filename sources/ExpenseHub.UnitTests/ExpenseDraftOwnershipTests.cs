using System;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class ExpenseDraftOwnershipTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 29, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly _expenseDate = new(2026, 9, 28);

    [TestMethod]
    internal void Create_UsesSuppliedAuthenticatedOwnerAndStartsDraft()
    {
        Expense expense = Expense.Create(
            "employee-1",
            "Business travel expense",
            100m,
            _expenseDate,
            null,
            _now);

        Assert.AreEqual("employee-1", expense.OwnerId);
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
        Assert.AreEqual(_now, expense.CreatedAtUtc);
        Assert.AreEqual(_now, expense.UpdatedAtUtc);
    }

    [TestMethod]
    internal void Edit_ByAnotherEmployee_IsForbiddenWithoutChangingData()
    {
        Expense expense = CreateDraft();

        Assert.ThrowsExactly<ForbiddenException>(() => expense.Edit(
            "employee-2",
            "Updated business expense",
            250m,
            _expenseDate,
            null,
            _now.AddMinutes(1)));

        Assert.AreEqual("Business travel expense", expense.Description);
        Assert.AreEqual(100m, expense.Amount);
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
    }

    [TestMethod]
    internal void Edit_OwnDraft_UpdatesDescriptionAndAmount()
    {
        Expense expense = CreateDraft();

        expense.Edit(
            "employee-1",
            "Updated business expense",
            250m,
            _expenseDate,
            null,
            _now.AddMinutes(1));

        Assert.AreEqual("Updated business expense", expense.Description);
        Assert.AreEqual(250m, expense.Amount);
        Assert.AreEqual(_now.AddMinutes(1), expense.UpdatedAtUtc);
    }

    [TestMethod]
    internal void Edit_SubmittedExpense_IsConflictAndPreservesData()
    {
        Expense expense = CreateDraft();
        expense.Submit("employee-1", _now.AddMinutes(1));

        Assert.ThrowsExactly<ConflictException>(() => expense.Edit(
            "employee-1",
            "Updated business expense",
            250m,
            _expenseDate,
            null,
            _now.AddMinutes(2)));

        Assert.AreEqual("Business travel expense", expense.Description);
        Assert.AreEqual(100m, expense.Amount);
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
    }

    private static Expense CreateDraft()
    {
        return Expense.Create(
            "employee-1",
            "Business travel expense",
            100m,
            _expenseDate,
            null,
            _now);
    }
}
