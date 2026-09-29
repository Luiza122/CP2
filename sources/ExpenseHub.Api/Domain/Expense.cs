using System;
using System.Collections.Generic;
using ExpenseHub.Api.Infrastructure;

namespace ExpenseHub.Api.Domain;

internal sealed class Expense
{
    public Guid Id { get; set; }

    public string OwnerId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateOnly ExpenseDate { get; set; }

    public ExpenseStatus Status { get; set; }

    public Guid? ExpenseCategoryId { get; set; }

    public ExpenseCategory? ExpenseCategory { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<ExpenseHistory> History { get; } = new List<ExpenseHistory>();

    public PaymentRecord? PaymentRecord { get; set; }

    public static Expense Create(
        string ownerId,
        string description,
        decimal amount,
        DateOnly expenseDate,
        Guid? categoryId,
        DateTimeOffset now)
    {
        ExpenseRules.ValidateFields(description, amount, expenseDate, now);

        return new Expense
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Description = description,
            Amount = amount,
            ExpenseDate = expenseDate,
            ExpenseCategoryId = categoryId,
            Status = ExpenseStatus.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public string Edit(
        string actorUserId,
        string description,
        decimal amount,
        DateOnly expenseDate,
        Guid? categoryId,
        DateTimeOffset now)
    {
        EnsureOwner(actorUserId);

        if (Status != ExpenseStatus.Draft)
        {
            throw new ConflictException("Only Draft expenses can be edited.");
        }

        ExpenseRules.ValidateFields(description, amount, expenseDate, now);

        List<string> changes = new();

        if (!string.Equals(Description, description, StringComparison.Ordinal))
        {
            changes.Add("description");
        }

        if (Amount != amount)
        {
            changes.Add("amount");
        }

        if (ExpenseDate != expenseDate)
        {
            changes.Add("expenseDate");
        }

        if (ExpenseCategoryId != categoryId)
        {
            changes.Add("categoryId");
        }

        Description = description;
        Amount = amount;
        ExpenseDate = expenseDate;
        ExpenseCategoryId = categoryId;
        UpdatedAtUtc = now;

        return changes.Count == 0 ? "no changes" : string.Join(", ", changes);
    }

    public void Submit(string actorUserId, DateTimeOffset now)
    {
        EnsureOwner(actorUserId);
        EnsureStatus(ExpenseStatus.Draft, "Only Draft expenses can be submitted.");
        Status = ExpenseStatus.Submitted;
        UpdatedAtUtc = now;
    }

    public void Approve(string actorUserId, DateTimeOffset now)
    {
        EnsureNotOwner(actorUserId, "The owner cannot approve their own expense.");
        EnsureStatus(ExpenseStatus.Submitted, "Only Submitted expenses can be approved.");
        Status = ExpenseStatus.Approved;
        UpdatedAtUtc = now;
    }

    public void Reject(string actorUserId, string justification, DateTimeOffset now)
    {
        EnsureNotOwner(actorUserId, "The owner cannot reject their own expense.");
        EnsureStatus(ExpenseStatus.Submitted, "Only Submitted expenses can be rejected.");
        ExpenseRules.ValidateJustification(justification);
        Status = ExpenseStatus.Rejected;
        UpdatedAtUtc = now;
    }

    public void Pay(string actorUserId, DateTimeOffset now)
    {
        EnsureNotOwner(actorUserId, "The owner cannot pay their own expense.");
        EnsureStatus(ExpenseStatus.Approved, "Only Approved expenses can be paid.");
        Status = ExpenseStatus.Paid;
        UpdatedAtUtc = now;
    }

    private void EnsureOwner(string actorUserId)
    {
        if (!string.Equals(OwnerId, actorUserId, StringComparison.Ordinal))
        {
            throw new ForbiddenException("Only the owner can perform this operation.");
        }
    }

    private void EnsureNotOwner(string actorUserId, string message)
    {
        if (string.Equals(OwnerId, actorUserId, StringComparison.Ordinal))
        {
            throw new ForbiddenException(message);
        }
    }

    private void EnsureStatus(ExpenseStatus requiredStatus, string message)
    {
        if (Status != requiredStatus)
        {
            throw new ConflictException(message);
        }
    }
}
