using System;
using System.Collections.Generic;

namespace ExpenseHub.Api.Domain;

internal static class ExpenseAccessPolicy
{
    public static bool HasRole(IReadOnlyCollection<string> roles, string role)
    {
        foreach (string currentRole in roles)
        {
            if (string.Equals(currentRole, role, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool CanRead(Expense expense, string userId, IReadOnlyCollection<string> roles)
    {
        if (HasRole(roles, Roles.Auditor))
        {
            return true;
        }

        if (HasRole(roles, Roles.Employee)
            && string.Equals(expense.OwnerId, userId, StringComparison.Ordinal))
        {
            return true;
        }

        if (HasRole(roles, Roles.Approver) && expense.Status == ExpenseStatus.Submitted)
        {
            return true;
        }

        return HasRole(roles, Roles.Finance)
            && (expense.Status == ExpenseStatus.Approved || expense.Status == ExpenseStatus.Paid);
    }
}
