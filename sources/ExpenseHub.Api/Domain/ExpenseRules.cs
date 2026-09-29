using System;
using ExpenseHub.Api.Infrastructure;

namespace ExpenseHub.Api.Domain;

internal static class ExpenseRules
{
    public const int MinimumTextLength = 10;
    public const int MaximumTextLength = 500;
    public const decimal MinimumAmount = 0.01m;
    public const decimal MaximumAmount = int.MaxValue;

    public static void ValidateFields(
        string? description,
        decimal amount,
        DateOnly expenseDate,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(description)
            || description.Length < MinimumTextLength
            || description.Length > MaximumTextLength)
        {
            throw new ValidationException("Description must contain between 10 and 500 characters.");
        }

        if (amount < MinimumAmount || amount > MaximumAmount)
        {
            throw new ValidationException("Amount must be between R$ 0.01 and R$ 2,147,483,647.00.");
        }

        DateOnly todayUtc = DateOnly.FromDateTime(now.UtcDateTime);

        if (expenseDate == DateOnly.MinValue || expenseDate > todayUtc)
        {
            throw new ValidationException("Expense date must be valid and cannot be in the future.");
        }
    }

    public static void ValidateJustification(string? justification)
    {
        if (string.IsNullOrWhiteSpace(justification)
            || justification.Length < MinimumTextLength
            || justification.Length > MaximumTextLength)
        {
            throw new ValidationException("Rejection justification must contain between 10 and 500 characters.");
        }
    }
}
