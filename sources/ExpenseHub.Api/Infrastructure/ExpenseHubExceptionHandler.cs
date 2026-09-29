using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Infrastructure;

public sealed class ExpenseHubExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ExpenseHubException expenseHubException)
        {
            return false;
        }

        ProblemDetails problem = new()
        {
            Status = expenseHubException.StatusCode,
            Title = expenseHubException.Title,
            Detail = expenseHubException.Message,
            Instance = httpContext.Request.Path,
        };

        httpContext.Response.StatusCode = expenseHubException.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
