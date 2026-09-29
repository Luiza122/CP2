using System;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
public sealed class ExpenseValidationAndHistoryTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 29, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void AmountBelowMinimum_IsRejected()
    {
        Assert.ThrowsExactly<ValidationException>(() => ExpenseRules.ValidateFields(
            "Valid expense description",
            0m,
            new DateOnly(2026, 9, 28),
            _now));
    }

    [TestMethod]
    public void AmountAboveMaximum_IsRejected()
    {
        Assert.ThrowsExactly<ValidationException>(() => ExpenseRules.ValidateFields(
            "Valid expense description",
            (decimal)int.MaxValue + 1m,
            new DateOnly(2026, 9, 28),
            _now));
    }

    [TestMethod]
    public void FutureExpenseDate_IsRejected()
    {
        Assert.ThrowsExactly<ValidationException>(() => ExpenseRules.ValidateFields(
            "Valid expense description",
            100m,
            new DateOnly(2026, 9, 30),
            _now));
    }

    [TestMethod]
    public void DescriptionOutsideAllowedLength_IsRejected()
    {
        Assert.ThrowsExactly<ValidationException>(() => ExpenseRules.ValidateFields(
            "short",
            100m,
            new DateOnly(2026, 9, 28),
            _now));
    }

    [TestMethod]
    public void History_StoresActorUtcStateAndJustification()
    {
        Expense expense = Expense.Create(
            "employee-1",
            "Valid business expense",
            100m,
            new DateOnly(2026, 9, 28),
            null,
            _now);
        expense.Submit("employee-1", _now.AddMinutes(1));
        expense.Reject("approver-1", "Expense policy was not satisfied.", _now.AddMinutes(2));

        ExpenseHistory history = ExpenseHistory.Create(
            expense,
            "Rejected",
            "approver-1",
            ExpenseStatus.Submitted,
            "Expense policy was not satisfied.",
            null,
            _now.AddMinutes(2));

        Assert.AreEqual("approver-1", history.ActorUserId);
        Assert.AreEqual(ExpenseStatus.Submitted, history.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Rejected, history.NewStatus);
        Assert.AreEqual("Expense policy was not satisfied.", history.Justification);
        Assert.AreEqual(TimeSpan.Zero, history.OccurredAtUtc.Offset);
    }
}
