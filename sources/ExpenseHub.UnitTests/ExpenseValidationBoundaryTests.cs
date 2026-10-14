using System;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class ExpenseValidationBoundaryTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 29, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly _today = new(2026, 9, 29);

    [TestMethod]
    internal void MinimumAmount_IsAccepted()
    {
        Expense expense = CreateDraft("Valid expense description", 0.01m, _today);

        Assert.AreEqual(0.01m, expense.Amount);
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
    }

    [TestMethod]
    internal void MaximumAmount_IsAccepted()
    {
        Expense expense = CreateDraft("Valid expense description", int.MaxValue, _today);

        Assert.AreEqual((decimal)int.MaxValue, expense.Amount);
    }

    [TestMethod]
    internal void MinimumDescriptionLength_IsAccepted()
    {
        Expense expense = CreateDraft("1234567890", 10m, _today);

        Assert.AreEqual("1234567890", expense.Description);
    }

    [TestMethod]
    internal void MaximumDescriptionLength_IsAccepted()
    {
        string description = new('a', ExpenseRules.MaximumTextLength);
        Expense expense = CreateDraft(description, 10m, _today);

        Assert.AreEqual(ExpenseRules.MaximumTextLength, expense.Description.Length);
    }

    [TestMethod]
    internal void TodayExpenseDate_IsAccepted()
    {
        Expense expense = CreateDraft("Valid expense description", 10m, _today);

        Assert.AreEqual(_today, expense.ExpenseDate);
    }

    [TestMethod]
    internal void JustificationBelowMinimum_IsRejected()
    {
        Assert.ThrowsExactly<ValidationException>(() => ExpenseRules.ValidateJustification("123456789"));
    }

    [TestMethod]
    internal void JustificationAtMaximumLength_IsAccepted()
    {
        Expense expense = CreateDraft("Valid expense description", 10m, _today);
        expense.Submit("employee-1", _now.AddMinutes(1));

        expense.Reject(
            "approver-1",
            new string('a', ExpenseRules.MaximumTextLength),
            _now.AddMinutes(2));

        Assert.AreEqual(ExpenseStatus.Rejected, expense.Status);
    }

    [TestMethod]
    internal void WhitespaceOnlyJustification_IsRejected()
    {
        Assert.ThrowsExactly<ValidationException>(() => ExpenseRules.ValidateJustification("          "));
    }

    private static Expense CreateDraft(string description, decimal amount, DateOnly expenseDate)
    {
        return Expense.Create(
            "employee-1",
            description,
            amount,
            expenseDate,
            null,
            _now);
    }
}
