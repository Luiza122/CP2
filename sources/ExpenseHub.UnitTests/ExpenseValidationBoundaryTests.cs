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
        ExpenseRules.ValidateFields("Valid expense description", 0.01m, _today, _now);
    }

    [TestMethod]
    internal void MaximumAmount_IsAccepted()
    {
        ExpenseRules.ValidateFields("Valid expense description", int.MaxValue, _today, _now);
    }

    [TestMethod]
    internal void MinimumDescriptionLength_IsAccepted()
    {
        ExpenseRules.ValidateFields("1234567890", 10m, _today, _now);
    }

    [TestMethod]
    internal void MaximumDescriptionLength_IsAccepted()
    {
        ExpenseRules.ValidateFields(new string('a', ExpenseRules.MaximumTextLength), 10m, _today, _now);
    }

    [TestMethod]
    internal void TodayExpenseDate_IsAccepted()
    {
        ExpenseRules.ValidateFields("Valid expense description", 10m, _today, _now);
    }

    [TestMethod]
    internal void JustificationBelowMinimum_IsRejected()
    {
        Assert.ThrowsExactly<ValidationException>(() => ExpenseRules.ValidateJustification("123456789"));
    }

    [TestMethod]
    internal void JustificationAtMaximumLength_IsAccepted()
    {
        ExpenseRules.ValidateJustification(new string('a', ExpenseRules.MaximumTextLength));
    }

    [TestMethod]
    internal void WhitespaceOnlyJustification_IsRejected()
    {
        Assert.ThrowsExactly<ValidationException>(() => ExpenseRules.ValidateJustification("          "));
    }
}
