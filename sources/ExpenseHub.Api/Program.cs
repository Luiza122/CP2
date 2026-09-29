using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Infrastructure;
using ExpenseHub.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExpenseHub.Api;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        string connectionString = builder.Configuration.GetConnectionString("ExpenseHub")
            ?? "Data Source=expensehub.db";

        builder.Services.AddDbContext<ExpenseHubDbContext>(options => options.UseSqlite(connectionString));
        builder.Services
            .AddIdentityApiEndpoints<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ExpenseHubDbContext>();
        builder.Services.AddAuthorization();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ExpenseHubExceptionHandler>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<ExpenseService>();
        builder.Services.AddOpenApi();
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        WebApplication app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .WithName("GetHealth");

        app.MapIdentityApi<ApplicationUser>();

        MapAdminEndpoints(app);
        MapExpenseEndpoints(app);

        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            await ExpenseHubSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
        }

        await app.RunAsync();
    }

    private static void MapAdminEndpoints(WebApplication app)
    {
        RouteGroupBuilder admin = app.MapGroup("/api/admin")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        admin.MapGet("/users", async (
            UserManager<ApplicationUser> userManager,
            CancellationToken cancellationToken) =>
        {
            List<ApplicationUser> users = await userManager.Users
                .OrderBy(user => user.Email)
                .ToListAsync(cancellationToken);
            List<UserResponse> response = new(users.Count);

            foreach (ApplicationUser user in users)
            {
                IList<string> roles = await userManager.GetRolesAsync(user);
                response.Add(new UserResponse(user.Id, user.Email ?? string.Empty, roles.ToArray()));
            }

            return Results.Ok(response);
        });

        admin.MapPut("/users/{id}/roles", async (
            string id,
            UpdateRolesRequest request,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager) =>
        {
            if (request.Roles is null)
            {
                return Results.BadRequest(new { error = "Roles are required." });
            }

            HashSet<string> requestedRoles = new(request.Roles, StringComparer.Ordinal);

            if (requestedRoles.Any(role => !Roles.All.Contains(role, StringComparer.Ordinal)))
            {
                return Results.BadRequest(new { error = "One or more roles are invalid." });
            }

            ApplicationUser? targetUser = await userManager.FindByIdAsync(id);

            if (targetUser is null)
            {
                return Results.NotFound();
            }

            string currentUserId = GetUserId(principal);

            if (string.Equals(currentUserId, targetUser.Id, StringComparison.Ordinal)
                && !requestedRoles.Contains(Roles.Admin))
            {
                return Results.BadRequest(new { error = "An Admin cannot remove their own Admin role." });
            }

            IList<string> currentRoles = await userManager.GetRolesAsync(targetUser);
            string[] rolesToRemove = currentRoles.Where(role => !requestedRoles.Contains(role)).ToArray();
            string[] rolesToAdd = requestedRoles
                .Where(role => !currentRoles.Contains(role, StringComparer.Ordinal))
                .ToArray();

            if (rolesToRemove.Length > 0)
            {
                IdentityResult removeResult = await userManager.RemoveFromRolesAsync(targetUser, rolesToRemove);

                if (!removeResult.Succeeded)
                {
                    return Results.BadRequest(new { error = "Unable to remove roles." });
                }
            }

            if (rolesToAdd.Length > 0)
            {
                IdentityResult addResult = await userManager.AddToRolesAsync(targetUser, rolesToAdd);

                if (!addResult.Succeeded)
                {
                    return Results.BadRequest(new { error = "Unable to add roles." });
                }
            }

            return Results.NoContent();
        });
    }

    private static void MapExpenseEndpoints(WebApplication app)
    {
        RouteGroupBuilder expenses = app.MapGroup("/api/expenses");

        expenses.MapPost(string.Empty, async (
            CreateExpenseRequest request,
            ClaimsPrincipal principal,
            ExpenseService service,
            CancellationToken cancellationToken) =>
        {
            ExpenseResponse expense = await service.CreateAsync(
                GetUserId(principal),
                request,
                cancellationToken);
            return Results.Created($"/api/expenses/{expense.Id}", expense);
        }).RequireAuthorization(policy => policy.RequireRole(Roles.Employee));

        expenses.MapPut("/{id:guid}", async (
            Guid id,
            UpdateExpenseRequest request,
            ClaimsPrincipal principal,
            ExpenseService service,
            CancellationToken cancellationToken) =>
        {
            ExpenseResponse expense = await service.UpdateAsync(
                id,
                GetUserId(principal),
                request,
                cancellationToken);
            return Results.Ok(expense);
        }).RequireAuthorization(policy => policy.RequireRole(Roles.Employee));

        expenses.MapGet(string.Empty, async (
            ClaimsPrincipal principal,
            ExpenseService service,
            CancellationToken cancellationToken) =>
        {
            IReadOnlyCollection<ExpenseResponse> result = await service.ListAsync(
                GetUserId(principal),
                GetRoles(principal),
                cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(policy =>
            policy.RequireRole(Roles.Employee, Roles.Approver, Roles.Finance, Roles.Auditor));

        expenses.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            ExpenseService service,
            CancellationToken cancellationToken) =>
        {
            ExpenseResponse result = await service.GetAsync(
                id,
                GetUserId(principal),
                GetRoles(principal),
                cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(policy =>
            policy.RequireRole(Roles.Employee, Roles.Approver, Roles.Finance, Roles.Auditor));

        expenses.MapPost("/{id:guid}/submit", async (
            Guid id,
            ClaimsPrincipal principal,
            ExpenseService service,
            CancellationToken cancellationToken) =>
        {
            ExpenseResponse result = await service.SubmitAsync(
                id,
                GetUserId(principal),
                cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(policy => policy.RequireRole(Roles.Employee));

        expenses.MapPost("/{id:guid}/approve", async (
            Guid id,
            ClaimsPrincipal principal,
            ExpenseService service,
            CancellationToken cancellationToken) =>
        {
            ExpenseResponse result = await service.ApproveAsync(
                id,
                GetUserId(principal),
                GetRoles(principal),
                cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(policy => policy.RequireRole(Roles.Approver));

        expenses.MapPost("/{id:guid}/reject", async (
            Guid id,
            RejectExpenseRequest request,
            ClaimsPrincipal principal,
            ExpenseService service,
            CancellationToken cancellationToken) =>
        {
            ExpenseResponse result = await service.RejectAsync(
                id,
                GetUserId(principal),
                GetRoles(principal),
                request,
                cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(policy => policy.RequireRole(Roles.Approver));

        expenses.MapPost("/{id:guid}/pay", async (
            Guid id,
            ClaimsPrincipal principal,
            ExpenseService service,
            CancellationToken cancellationToken) =>
        {
            ExpenseResponse result = await service.PayAsync(
                id,
                GetUserId(principal),
                GetRoles(principal),
                cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(policy => policy.RequireRole(Roles.Finance));

        expenses.MapGet("/{id:guid}/history", async (
            Guid id,
            ClaimsPrincipal principal,
            ExpenseService service,
            CancellationToken cancellationToken) =>
        {
            IReadOnlyCollection<ExpenseHistoryResponse> result = await service.GetHistoryAsync(
                id,
                GetUserId(principal),
                GetRoles(principal),
                cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(policy =>
            policy.RequireRole(Roles.Employee, Roles.Approver, Roles.Finance, Roles.Auditor));
    }

    private static string GetUserId(ClaimsPrincipal principal)
    {
        string? userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return userId ?? throw new InvalidOperationException("Authenticated user identifier is missing.");
    }

    private static IReadOnlyCollection<string> GetRoles(ClaimsPrincipal principal)
    {
        return principal.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToArray();
    }
}
