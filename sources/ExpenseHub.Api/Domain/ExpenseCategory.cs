using System;
using System.Collections.Generic;

namespace ExpenseHub.Api.Domain;

internal sealed class ExpenseCategory
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Expense> Expenses { get; } = new List<Expense>();
}
