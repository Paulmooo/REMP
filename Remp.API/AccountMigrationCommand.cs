using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Remp.DataAccess.Data;
using Remp.Models.Entities;
using Remp.Repository.Repositories;
using Remp.Service.Services;

namespace Remp.API;

public static class AccountMigrationCommand
{
    public static async Task RunAsync(string[] args)
    {
        if (args.Length is < 1 or > 2 || (args.Length == 2 && args[1] != "--apply"))
            throw new ArgumentException("Usage: migrate-users <mapping.json> [--apply]. Default: validation only.");
        var mappings = JsonSerializer.Deserialize<List<AgentAccountMapping>>(await File.ReadAllTextAsync(args[0]),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new ArgumentException("Invalid mapping file.");
        var connection = Environment.GetEnvironmentVariable("REMP_MIGRATION_CONNECTION")
            ?? throw new ArgumentException("Set REMP_MIGRATION_CONNECTION explicitly for the intended maintenance database.");
        var services = new ServiceCollection();
        // No application configuration, request logging, Blob client or automatic seeding/migrations.
        services.AddLogging();
        services.AddDbContext<RempDbContext>(o => o.UseSqlServer(connection));
        services.AddIdentityCore<User>(o => o.User.RequireUniqueEmail = true).AddRoles<Role>().AddEntityFrameworkStores<RempDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var service = new AgentAccountMigrationService(new AgentAccountMigrationRepository(
            scope.ServiceProvider.GetRequiredService<RempDbContext>(), scope.ServiceProvider.GetRequiredService<UserManager<User>>()));
        var count = await service.MigrateAsync(mappings, args.Length == 2);
        Console.WriteLine($"{(args.Length == 2 ? "Migrated" : "Validated (no changes)")}: {count} mapped accounts. Unmapped accounts are unchanged.");
    }
}
