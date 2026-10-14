using System;
using System.Collections.Generic;
using ExpenseHub.Api.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class RoleManagementRulesTests
{
    [TestMethod]
    internal void ContainsOnlyKnownRoles_AcceptsDefinedRoles()
    {
        Assert.IsTrue(RoleManagementRules.ContainsOnlyKnownRoles(
            new[] { Roles.Employee, Roles.Approver }));
    }

    [TestMethod]
    internal void ContainsOnlyKnownRoles_RejectsUnknownRole()
    {
        Assert.IsFalse(RoleManagementRules.ContainsOnlyKnownRoles(
            new[] { Roles.Employee, "SuperAdmin" }));
    }

    [TestMethod]
    internal void ContainsOnlyKnownRoles_RejectsNull()
    {
        Assert.IsFalse(RoleManagementRules.ContainsOnlyKnownRoles(null));
    }

    [TestMethod]
    internal void CanPreserveOwnAdminRole_RejectsSelfRemoval()
    {
        IReadOnlyCollection<string> requestedRoles = new[] { Roles.Employee };

        Assert.IsFalse(RoleManagementRules.CanPreserveOwnAdminRole(
            "admin-1",
            "admin-1",
            requestedRoles));
    }

    [TestMethod]
    internal void CanPreserveOwnAdminRole_AllowsAdminRoleToRemain()
    {
        IReadOnlyCollection<string> requestedRoles = new[] { Roles.Admin, Roles.Employee };

        Assert.IsTrue(RoleManagementRules.CanPreserveOwnAdminRole(
            "admin-1",
            "admin-1",
            requestedRoles));
    }

    [TestMethod]
    internal void CanPreserveOwnAdminRole_AllowsAdminToEditAnotherUser()
    {
        IReadOnlyCollection<string> requestedRoles = new[] { Roles.Employee };

        Assert.IsTrue(RoleManagementRules.CanPreserveOwnAdminRole(
            "admin-1",
            "employee-2",
            requestedRoles));
    }
}
