using System;

namespace ExpenseHub.Api.Infrastructure;

internal abstract class ExpenseHubException : Exception
{
    protected ExpenseHubException(int statusCode, string title, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Title = title;
    }

    public int StatusCode { get; }

    public string Title { get; }
}

internal sealed class ValidationException : ExpenseHubException
{
    public ValidationException(string message)
        : base(400, "Validation error", message)
    {
    }
}

internal sealed class ForbiddenException : ExpenseHubException
{
    public ForbiddenException(string message)
        : base(403, "Forbidden", message)
    {
    }
}

internal sealed class NotFoundException : ExpenseHubException
{
    public NotFoundException(string message)
        : base(404, "Not found", message)
    {
    }
}

internal sealed class ConflictException : ExpenseHubException
{
    public ConflictException(string message)
        : base(409, "Conflict", message)
    {
    }
}
