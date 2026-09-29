using System;
using System.Collections.Generic;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Contracts;

internal sealed record CreateExpenseRequest(
    string? Description,
    decimal Amount,
    DateOnly ExpenseDate,
    Guid? CategoryId);

internal sealed record UpdateExpenseRequest(
    string? Description,
    decimal Amount,
    DateOnly ExpenseDate,
    Guid? CategoryId);

internal sealed record RejectExpenseRequest(string? Justification);

internal sealed record UpdateRolesRequest(IReadOnlyCollection<string>? Roles);

internal sealed record ExpenseResponse(
    Guid Id,
    string OwnerId,
    string Description,
    decimal Amount,
    DateOnly ExpenseDate,
    ExpenseStatus Status,
    Guid? CategoryId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

internal sealed record ExpenseHistoryResponse(
    Guid Id,
    string Action,
    string ActorUserId,
    DateTimeOffset OccurredAtUtc,
    ExpenseStatus? PreviousStatus,
    ExpenseStatus NewStatus,
    string? Justification,
    string? Changes);

internal sealed record UserResponse(string Id, string Email, IReadOnlyCollection<string> Roles);
