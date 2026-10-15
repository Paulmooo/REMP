using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Remp.Common.Exceptions;
using Remp.DataAccess.Data;
using Remp.Models.Entities;
using Remp.Repository.Interfaces;

namespace Remp.Repository.Repositories;

public sealed class AgentAccountMigrationRepository(RempDbContext db, UserManager<User> users)
    : IAgentAccountMigrationRepository
{
    public async Task<int> MigrateAsync(IReadOnlyList<AgentAccountMapping> mappings, bool apply)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var accounts = new List<(AgentAccountMapping Mapping, User User, PhotographyCompany Company)>();
            foreach (var mapping in mappings)
            {
                var user = await users.FindByIdAsync(mapping.UserId)
                    ?? throw new KeyNotFoundException("Mapped user not found.");
                var roles = await users.GetRolesAsync(user);
                if (user.IsDeleted || !roles.Contains("User") || roles.Contains("Admin") || roles.Contains("Agent") ||
                    await db.Agents.AnyAsync(a => a.Id == user.Id) ||
                    await db.PhotographyCompanies.AnyAsync(c => c.Id == user.Id))
                    throw new ConflictException("Mapped source must be an active User account without a business profile.");
                if (string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrWhiteSpace(user.NormalizedEmail) ||
                    !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(user.Email) ||
                    await db.Users.CountAsync(u => u.NormalizedEmail == user.NormalizedEmail) != 1)
                    throw new ConflictException("Mapped source must have a valid unique normalized email.");
                var company = await db.PhotographyCompanies.Include(c => c.User).Include(c => c.Agents)
                    .SingleOrDefaultAsync(c => c.Id == mapping.CompanyId)
                    ?? throw new KeyNotFoundException("Mapped photography company not found.");
                if (company.User.IsDeleted || !await users.IsInRoleAsync(company.User, "Admin"))
                    throw new ConflictException("Mapped company must be an active Admin photography company account.");
                accounts.Add((mapping, user, company));
            }

            if (!apply)
            {
                await transaction.RollbackAsync();
                db.ChangeTracker.Clear();
                return accounts.Count;
            }

            // One batch transaction preserves passwords, IDs and all existing business rows.
            foreach (var (mapping, user, company) in accounts)
            {
                EnsureSucceeded(await users.AddToRoleAsync(user, "Agent"));
                EnsureSucceeded(await users.RemoveFromRoleAsync(user, "User"));
                var agent = new Agent
                {
                    Id = user.Id, User = user, AgentFirstName = mapping.FirstName, AgentLastName = mapping.LastName,
                    AvatarUrl = mapping.AvatarUrl ?? string.Empty, CompanyName = company.PhotographyCompanyName
                };
                db.Agents.Add(agent);
                company.Agents.Add(agent);
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return accounts.Count;
        }
        catch
        {
            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new ConflictException("Identity rejected the mapped account migration; the batch was rolled back.");
    }
}
