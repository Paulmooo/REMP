using System;
using Microsoft.AspNetCore.Identity;
using Remp.Models.Entities;

namespace Remp.API;

public static class DbSeeder
{
    // 后续可以把方法扩成一个总入口
    // public static async Task SeedAsync(IServiceProvider services)
    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<Role>>();

        var roles = new[]
        {
            "Admin",
            "Agent",
            "User"
        };

        foreach (var roleName in roles)
        {
            var exists = await roleManager.RoleExistsAsync(roleName);
            if (exists) continue;

            await roleManager.CreateAsync(new Role
            {
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant()
            });
        }
    }
}
