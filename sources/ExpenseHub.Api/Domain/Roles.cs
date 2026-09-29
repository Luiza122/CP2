using System;
using System.Collections.Generic;

namespace ExpenseHub.Api.Domain;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Employee = "Employee";
    public const string Approver = "Approver";
    public const string Finance = "Finance";
    public const string Auditor = "Auditor";

    public static IReadOnlyCollection<string> All { get; } =
        Array.AsReadOnly([Admin, Employee, Approver, Finance, Auditor]);
}
