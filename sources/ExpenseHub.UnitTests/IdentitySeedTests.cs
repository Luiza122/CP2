using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class IdentitySeedTests
{
    private const string AdminEmail = "seed-admin@test.local";
    private const string AdminPassword = "SeedTest!Password123";

    [TestMethod]
    internal async Task SeedAsync_CreatesConfiguredAdminAndAllRequiredRoles()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using ServiceProvider provider = CreateProvider(connection);
        IConfiguration configuration = CreateConfiguration();

        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        await ExpenseHubSeeder.SeedAsync(scope.ServiceProvider, configuration);

        UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        RoleManager<IdentityRole> roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        ApplicationUser? admin = await users.FindByEmailAsync(AdminEmail);

        Assert.IsNotNull(admin);
        Assert.IsTrue(await users.CheckPasswordAsync(admin!, AdminPassword));
        Assert.IsTrue(await users.IsInRoleAsync(admin!, Roles.Admin));

        foreach (string role in Roles.All)
        {
            Assert.IsTrue(await roles.RoleExistsAsync(role), $"Missing required role: {role}");
        }
    }

    [TestMethod]
    internal async Task SeedAsync_IsIdempotentAndDoesNotDuplicateAdminOrRoles()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using ServiceProvider provider = CreateProvider(connection);
        IConfiguration configuration = CreateConfiguration();

        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        await ExpenseHubSeeder.SeedAsync(scope.ServiceProvider, configuration);
        await ExpenseHubSeeder.SeedAsync(scope.ServiceProvider, configuration);

        ExpenseHubDbContext dbContext = scope.ServiceProvider.GetRequiredService<ExpenseHubDbContext>();
        UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IList<ApplicationUser> adminUsers = await users.GetUsersInRoleAsync(Roles.Admin);
        int roleCount = await dbContext.Roles.CountAsync();

        Assert.AreEqual(1, adminUsers.Count);
        Assert.AreEqual(Roles.All.Count, roleCount);
        Assert.AreEqual(1, await dbContext.Users.CountAsync());
    }

    private static ServiceProvider CreateProvider(SqliteConnection connection)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddDbContext<ExpenseHubDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityApiEndpoints<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ExpenseHubDbContext>();
        return services.BuildServiceProvider();
    }

    private static IConfiguration CreateConfiguration()
    {
        Dictionary<string, string?> values = new()
        {
            ["ExpenseHub:AdminEmail"] = AdminEmail,
            ["ExpenseHub:AdminPassword"] = AdminPassword,
        };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
