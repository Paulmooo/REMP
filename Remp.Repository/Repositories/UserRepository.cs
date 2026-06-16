using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Remp.DataAccess.Data;
using Remp.Models.Entities;
using Remp.Repository.Interfaces;

namespace Remp.Repository.Repositories;

public class UserRepository : IUserRepository
{
    private readonly RempDbContext _dbContext;
    private readonly UserManager<User> _userManager;

    public UserRepository(RempDbContext dbContext, UserManager<User> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<User?> GetUserByIdAsync(string userId)
    {
        return await _userManager.FindByIdAsync(userId);
    }

    public async Task<List<string>> GetRolesByUserIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return [];
        }

        return (await _userManager.GetRolesAsync(user))
            .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
            .Distinct()
            .ToList();
    }

    public async Task<List<ListingCase>> GetListingCasesByUserIdAdminAsync(string userId)
    {
        return await _dbContext.ListingCases
            .Where(x => x.UserId == userId)
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<ListingCase>> GetListingCasesByUserIdAgentAsync(string userId)
    {
        return await _dbContext.ListingCases
            .Where(x => x.Agents.Any(a => a.Id == userId))
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<Agent?> GetAgentByIdAsync(string userId)
    {
        return await _dbContext.Agents
            .FirstOrDefaultAsync(a => a.Id == userId);
    }

    public async Task<PhotographyCompany?> GetPhotographyCompanyByIdAsync(string companyId)
    {
        return await _dbContext.PhotographyCompanies
            .Include(c => c.Agents)
            .ThenInclude(a => a.User)
            .FirstOrDefaultAsync(c => c.Id == companyId);
    }

    public async Task AddAgentToPhotographyCompany(PhotographyCompany company, Agent agent)
    {
        company.Agents.Add(agent);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<IdentityResult> CreateAgentAsync(Agent agent, User user, string password)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return createResult;
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "Agent");
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return roleResult;
            }

            var agentResult = await _dbContext.Agents.AddAsync(agent);
            if (agentResult == null)
            {
                await transaction.RollbackAsync();
                return IdentityResult.Failed(new IdentityError { Description = "Failed to create agent entity" });
            }
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();
            return IdentityResult.Success;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Agent?> GetAgentByEmailAsync(string email)
    {
        return await _dbContext.Agents
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.User.Email == email);
    }

    public async Task<IdentityResult> ChangePasswordAsync(User user, string currentPassword, string newPassword)
    {
        return await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
    }
}
