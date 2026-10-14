using System;
using System.Collections.Generic;
using System.Linq;

namespace ExpenseHub.Api.Domain;

internal static class RoleManagementRules
{
    public static bool ContainsOnlyKnownRoles(IReadOnlyCollection<string>? requestedRoles)
    {
        return requestedRoles is not null
            && requestedRoles.All(role => Roles.All.Contains(role, StringComparer.Ordinal));
    }

    public static bool CanPreserveOwnAdminRole(
        string currentUserId,
        string targetUserId,
        IReadOnlyCollection<string> requestedRoles)
    {
        return !string.Equals(currentUserId, targetUserId, StringComparison.Ordinal)
            || requestedRoles.Contains(Roles.Admin, StringComparer.Ordinal);
    }
}
