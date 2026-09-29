using System;

namespace ExpenseHub.Api.Domain;

internal sealed class PaymentRecord
{
    public Guid Id { get; set; }

    public Guid ExpenseId { get; set; }

    public Expense? Expense { get; set; }

    public string ActorUserId { get; set; } = string.Empty;

    public DateTimeOffset PaidAtUtc { get; set; }
}
