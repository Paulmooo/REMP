using System;
using Microsoft.EntityFrameworkCore;
using Recam.Models;
using Recam.Models.Entities;

namespace Recam.API.DataAccess;

public class RecamDbContext : DbContext
{
    public DbSet<Agent> Agents { get; set; }
    public DbSet<CaseContact> CaseContacts { get; set; }
    public DbSet<ListingCase> ListingCases { get; set; }
    public DbSet<MediaAsset> MediaAssets { get; set; }
    public DbSet<PhotograpyCompany> PhotographyCompanies { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<User> Users { get; set; }

    public RecamDbContext(DbContextOptions<RecamDbContext> options) : base(options)
    {
    }
}
