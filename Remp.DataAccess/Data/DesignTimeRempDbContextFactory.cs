using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Remp.DataAccess.Data;

public class DesignTimeRempDbContextFactory : IDesignTimeDbContextFactory<RempDbContext>
{
    public RempDbContext CreateDbContext(string[] args)
    {
        string apiProjectPath = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Remp.API"));

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json")
            .Build();
        
        var builder = new DbContextOptionsBuilder<RempDbContext>();
        var connectionString = configuration.GetConnectionString("RempDb");

        builder.UseSqlServer(connectionString);
        return new RempDbContext(builder.Options);
    }
}
