using System;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Recam.Models.Entities;

namespace Recam.DataAccess.Data;

public class RecamDbContext : IdentityDbContext<User, Role, string>
{
    public DbSet<Agent> Agents { get; set; }
    public DbSet<CaseContact> CaseContacts { get; set; }
    public DbSet<ListingCase> ListingCases { get; set; }
    public DbSet<MediaAsset> MediaAssets { get; set; }
    public DbSet<PhotographyCompany> PhotographyCompanies { get; set; }

    public RecamDbContext(DbContextOptions<RecamDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Agent>()
            .HasOne(agent => agent.User)
            .WithOne(user => user.Agent)
            .HasForeignKey<Agent>(agent => agent.Id)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<PhotographyCompany>()
            .HasOne(company => company.User)
            .WithOne(user => user.PhotographyCompany)
            .HasForeignKey<PhotographyCompany>(company => company.Id)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
