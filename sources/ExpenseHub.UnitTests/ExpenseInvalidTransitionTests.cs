using System;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class ExpenseInvalidTransitionTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 29, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    internal void Submit_WhenAlreadySubmitted_IsConflict()
    {
        Expense expense = CreateSubmitted();

        Assert.ThrowsExactly<ConflictException>(() => expense.Submit(
            "employee-1",
            _now.AddMinutes(2)));

        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
    }

    [TestMethod]
    internal void Approve_FromDraft_IsConflict()
    {
        Expense expense = CreateDraft();

        Assert.ThrowsExactly<ConflictException>(() => expense.Approve(
            "approver-1",
            _now.AddMinutes(1)));

        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
    }

    [TestMethod]
    internal void Reject_FromDraft_IsConflict()
    {
        Expense expense = CreateDraft();

        Assert.ThrowsExactly<ConflictException>(() => expense.Reject(
            "approver-1",
            "Valid rejection justification.",
            _now.AddMinutes(1)));

        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
    }

    [TestMethod]
    internal void Pay_FromSubmitted_IsConflict()
    {
        Expense expense = CreateSubmitted();

        Assert.ThrowsExactly<ConflictException>(() => expense.Pay(
            "finance-1",
            _now.AddMinutes(2)));

        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
    }

    [TestMethod]
    internal void Edit_DraftByDifferentOwner_IsForbidden()
    {
        Expense expense = CreateDraft();

        Assert.ThrowsExactly<ForbiddenException>(() => expense.Edit(
            "employee-2",
            "Updated business expense",
            200m,
            new DateOnly(2026, 9, 28),
            null,
            _now.AddMinutes(1)));

        Assert.AreEqual("Business travel expense", expense.Description);
        Assert.AreEqual(100m, expense.Amount);
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
    }

    private static Expense CreateDraft()
    {
        return Expense.Create(
            "employee-1",
            "Business travel expense",
            100m,
            new DateOnly(2026, 9, 28),
            null,
            _now);
    }

    private static Expense CreateSubmitted()
    {
        Expense expense = CreateDraft();
        expense.Submit("employee-1", _now.AddMinutes(1));
        return expense;
    }
}
