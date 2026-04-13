using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Recam.DataAccess.Data;
using Recam.Models.Entities;
using Recam.Repository.Interfaces;

namespace Recam.Repository.Repositories;

public class UserRepository : IUserRepository
{
    private readonly RecamDbContext _dbContext;
    private readonly UserManager<User> _userManager;

    public UserRepository(RecamDbContext dbContext, UserManager<User> userManager)
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
}
