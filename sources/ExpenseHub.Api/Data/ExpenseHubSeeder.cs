using System;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExpenseHub.Api.Data;

public static class ExpenseHubSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        ExpenseHubDbContext dbContext = services.GetRequiredService<ExpenseHubDbContext>();
        RoleManager<IdentityRole> roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        UserManager<ApplicationUser> userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        ILoggerFactory loggerFactory = services.GetRequiredService<ILoggerFactory>();
        ILogger logger = loggerFactory.CreateLogger("ExpenseHubSeeder");

        await dbContext.Database.EnsureCreatedAsync();

        foreach (string role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                IdentityResult roleResult = await roleManager.CreateAsync(new IdentityRole(role));

                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException($"Unable to create required role '{role}'.");
                }
            }
        }

        if (!await dbContext.ExpenseCategories.AnyAsync())
        {
            dbContext.ExpenseCategories.Add(new ExpenseCategory
            {
                Id = Guid.Parse("7d690e44-0ea0-43c6-8cf0-2935201fc50b"),
                Name = "General",
            });
            await dbContext.SaveChangesAsync();
        }

        string adminEmail = configuration["ExpenseHub:AdminEmail"] ?? "admin@expensehub.local";
        string? adminPassword = configuration["ExpenseHub:AdminPassword"];

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning(
                "Admin seed skipped because ExpenseHub:AdminPassword is not configured. " +
                "Set ExpenseHub__AdminPassword before the first run.");
            return;
        }

        ApplicationUser? admin = await userManager.FindByEmailAsync(adminEmail);

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
            };

            IdentityResult createResult = await userManager.CreateAsync(admin, adminPassword);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException("Unable to create the initial Admin account.");
            }
        }

        if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
        {
            IdentityResult addRoleResult = await userManager.AddToRoleAsync(admin, Roles.Admin);

            if (!addRoleResult.Succeeded)
            {
                throw new InvalidOperationException("Unable to assign the Admin role to the initial account.");
            }
        }
    }
}
