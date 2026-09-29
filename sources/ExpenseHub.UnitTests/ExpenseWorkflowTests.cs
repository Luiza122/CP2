using System;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class ExpenseWorkflowTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 29, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    internal void Create_StartsAsDraftWithAuthenticatedOwner()
    {
        Expense expense = CreateDraft("employee-1");

        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
        Assert.AreEqual("employee-1", expense.OwnerId);
        Assert.AreEqual(_now, expense.CreatedAtUtc);
    }

    [TestMethod]
    internal void Submit_ChangesDraftToSubmitted()
    {
        Expense expense = CreateDraft("employee-1");

        expense.Submit("employee-1", _now.AddMinutes(1));

        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
    }

    [TestMethod]
    internal void Submit_ByDifferentOwner_IsForbidden()
    {
        Expense expense = CreateDraft("employee-1");

        Assert.ThrowsExactly<ForbiddenException>(() => expense.Submit("employee-2", _now.AddMinutes(1)));
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
    }

    [TestMethod]
    internal void Approve_ByOwner_IsForbidden()
    {
        Expense expense = CreateSubmitted("employee-1");

        Assert.ThrowsExactly<ForbiddenException>(() => expense.Approve("employee-1", _now.AddMinutes(2)));
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
    }

    [TestMethod]
    internal void Approve_ChangesSubmittedToApproved()
    {
        Expense expense = CreateSubmitted("employee-1");

        expense.Approve("approver-1", _now.AddMinutes(2));

        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
    }

    [TestMethod]
    internal void Reject_RequiresValidJustification()
    {
        Expense expense = CreateSubmitted("employee-1");

        Assert.ThrowsExactly<ValidationException>(() => expense.Reject("approver-1", "short", _now.AddMinutes(2)));
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
    }

    [TestMethod]
    internal void Reject_IsFinalAndCannotBeApprovedLater()
    {
        Expense expense = CreateSubmitted("employee-1");
        expense.Reject("approver-1", "Expense policy was not satisfied.", _now.AddMinutes(2));

        Assert.ThrowsExactly<ConflictException>(() => expense.Approve("approver-2", _now.AddMinutes(3)));
        Assert.AreEqual(ExpenseStatus.Rejected, expense.Status);
    }

    [TestMethod]
    internal void Pay_ByOwner_IsForbidden()
    {
        Expense expense = CreateApproved("employee-1");

        Assert.ThrowsExactly<ForbiddenException>(() => expense.Pay("employee-1", _now.AddMinutes(3)));
        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
    }

    [TestMethod]
    internal void Pay_ChangesApprovedToPaidAndCannotRepeat()
    {
        Expense expense = CreateApproved("employee-1");
        expense.Pay("finance-1", _now.AddMinutes(3));

        Assert.AreEqual(ExpenseStatus.Paid, expense.Status);
        Assert.ThrowsExactly<ConflictException>(() => expense.Pay("finance-2", _now.AddMinutes(4)));
    }

    [TestMethod]
    internal void Edit_IsAllowedOnlyWhileDraftAndByOwner()
    {
        Expense expense = CreateDraft("employee-1");
        string changes = expense.Edit(
            "employee-1",
            "Updated business expense",
            250m,
            new DateOnly(2026, 9, 28),
            null,
            _now.AddMinutes(1));

        Assert.IsTrue(changes.Contains("description", StringComparison.Ordinal));
        expense.Submit("employee-1", _now.AddMinutes(2));
        Assert.ThrowsExactly<ConflictException>(() => expense.Edit(
            "employee-1",
            "Another valid description",
            300m,
            new DateOnly(2026, 9, 28),
            null,
            _now.AddMinutes(3)));
    }

    private static Expense CreateDraft(string ownerId)
    {
        return Expense.Create(
            ownerId,
            "Business travel expense",
            100m,
            new DateOnly(2026, 9, 28),
            null,
            _now);
    }

    private static Expense CreateSubmitted(string ownerId)
    {
        Expense expense = CreateDraft(ownerId);
        expense.Submit(ownerId, _now.AddMinutes(1));
        return expense;
    }

    private static Expense CreateApproved(string ownerId)
    {
        Expense expense = CreateSubmitted(ownerId);
        expense.Approve("approver-1", _now.AddMinutes(2));
        return expense;
    }
}
