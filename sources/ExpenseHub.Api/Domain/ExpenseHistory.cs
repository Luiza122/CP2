using System;

namespace ExpenseHub.Api.Domain;

public sealed class ExpenseHistory
{
    public Guid Id { get; set; }

    public Guid ExpenseId { get; set; }

    public Expense? Expense { get; set; }

    public string Action { get; set; } = string.Empty;

    public string ActorUserId { get; set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; set; }

    public ExpenseStatus? PreviousStatus { get; set; }

    public ExpenseStatus NewStatus { get; set; }

    public string? Justification { get; set; }

    public string? Changes { get; set; }

    public static ExpenseHistory Create(
        Expense expense,
        string action,
        string actorUserId,
        ExpenseStatus? previousStatus,
        string? justification,
        string? changes,
        DateTimeOffset now)
    {
        return new ExpenseHistory
        {
            Id = Guid.NewGuid(),
            ExpenseId = expense.Id,
            Action = action,
            ActorUserId = actorUserId,
            OccurredAtUtc = now,
            PreviousStatus = previousStatus,
            NewStatus = expense.Status,
            Justification = justification,
            Changes = changes,
        };
    }
}
