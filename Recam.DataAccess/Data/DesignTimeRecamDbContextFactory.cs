using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Recam.DataAccess.Data;

public class DesignTimeRecamDbContextFactory : IDesignTimeDbContextFactory<RecamDbContext>
{
    public RecamDbContext CreateDbContext(string[] args)
    {
        string apiProjectPath = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Recam.API"));

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json")
            .Build();
        
        var builder = new DbContextOptionsBuilder<RecamDbContext>();
        var connectionString = configuration.GetConnectionString("RecamDb");

        builder.UseSqlServer(connectionString);
        return new RecamDbContext(builder.Options);
    }
}
