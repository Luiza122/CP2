using System;
using System.Collections.Generic;
using ExpenseHub.Api.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class ExpenseAccessPolicyTests
{
    [TestMethod]
    internal void Employee_CanReadOnlyOwnExpense()
    {
        Expense ownExpense = CreateExpense("employee-1", ExpenseStatus.Draft);
        Expense otherExpense = CreateExpense("employee-2", ExpenseStatus.Draft);
        IReadOnlyCollection<string> roles = [Roles.Employee];

        Assert.IsTrue(ExpenseAccessPolicy.CanRead(ownExpense, "employee-1", roles));
        Assert.IsFalse(ExpenseAccessPolicy.CanRead(otherExpense, "employee-1", roles));
    }

    [TestMethod]
    internal void Approver_CanReadSubmittedExpense()
    {
        Expense expense = CreateExpense("employee-1", ExpenseStatus.Submitted);

        Assert.IsTrue(ExpenseAccessPolicy.CanRead(expense, "approver-1", [Roles.Approver]));
    }

    [TestMethod]
    internal void Finance_CanReadOnlyApprovedOrPaidExpense()
    {
        Expense approved = CreateExpense("employee-1", ExpenseStatus.Approved);
        Expense submitted = CreateExpense("employee-1", ExpenseStatus.Submitted);
        IReadOnlyCollection<string> roles = [Roles.Finance];

        Assert.IsTrue(ExpenseAccessPolicy.CanRead(approved, "finance-1", roles));
        Assert.IsFalse(ExpenseAccessPolicy.CanRead(submitted, "finance-1", roles));
    }

    [TestMethod]
    internal void Auditor_CanReadEveryState()
    {
        Expense expense = CreateExpense("employee-1", ExpenseStatus.Rejected);

        Assert.IsTrue(ExpenseAccessPolicy.CanRead(expense, "auditor-1", [Roles.Auditor]));
    }

    [TestMethod]
    internal void AdminRoleAlone_DoesNotGrantExpenseReadAccess()
    {
        Expense expense = CreateExpense("employee-1", ExpenseStatus.Submitted);

        Assert.IsFalse(ExpenseAccessPolicy.CanRead(expense, "admin-1", [Roles.Admin]));
    }

    [TestMethod]
    internal void MultipleRoles_CombineReadPermissions()
    {
        Expense ownDraft = CreateExpense("employee-1", ExpenseStatus.Draft);
        Expense submitted = CreateExpense("employee-2", ExpenseStatus.Submitted);
        IReadOnlyCollection<string> roles = [Roles.Employee, Roles.Approver];

        Assert.IsTrue(ExpenseAccessPolicy.CanRead(ownDraft, "employee-1", roles));
        Assert.IsTrue(ExpenseAccessPolicy.CanRead(submitted, "employee-1", roles));
    }

    private static Expense CreateExpense(string ownerId, ExpenseStatus status)
    {
        return new Expense
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Description = "Valid expense description",
            Amount = 100m,
            ExpenseDate = new DateOnly(2026, 9, 28),
            Status = status,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
