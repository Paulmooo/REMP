using System;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Remp.Models.Entities;

namespace Remp.DataAccess.Data;

public class RempDbContext : IdentityDbContext<User, Role, string>
{
    public DbSet<Agent> Agents { get; set; }
    public DbSet<CaseContact> CaseContacts { get; set; }
    public DbSet<ListingCase> ListingCases { get; set; }
    public DbSet<MediaAsset> MediaAssets { get; set; }
    public DbSet<PhotographyCompany> PhotographyCompanies { get; set; }

    public RempDbContext(DbContextOptions<RempDbContext> options) : base(options)
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

        builder.Entity<Agent>()
            .HasMany(agent => agent.PhotographyCompanies)
            .WithMany(company => company.Agents)
            .UsingEntity(joinEntity => joinEntity.ToTable("AgentPhotograpyCompany"));

        builder.Entity<ListingCase>()
            .Property(listingCase => listingCase.Latitude)
            .HasColumnType("decimal(18,2)");

        builder.Entity<ListingCase>()
            .Property(listingCase => listingCase.Longitude)
            .HasColumnType("decimal(18,2)");
    }
}
