using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Remp.DataAccess.Data;
using Remp.Models.Entities;
using Remp.Repository.Interfaces;

namespace Remp.Repository.Repositories;

public class AuthRepository : IAuthRepository
{
    private readonly RempDbContext _dbContext;
    private readonly UserManager<User> _userManager;

    public AuthRepository(RempDbContext dbContext, UserManager<User> userManager)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }

    public async Task<User?> FindByUsernameAsync(string username)
    {
        return await _userManager.FindByNameAsync(username);
    }

    public async Task<IdentityResult> RegisterWithRoleAsync(User user, string password, string role)
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

            var roleResult = await _userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return roleResult;
            }

            await transaction.CommitAsync();
            return IdentityResult.Success;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<string>> GetRolesAsync(User user)
    {
        return (await _userManager.GetRolesAsync(user)).ToList();
    }

    public async Task<bool> CheckPasswordAsync(User user, string password)
    {
        return await _userManager.CheckPasswordAsync(user, password);
    }

    public async Task<int> GetUserCountAsync()
    {
        return await _userManager.Users.CountAsync();
    }

    public async Task<List<User>> GetUsersPagedAsync(int skip, int take)
    {
        return await _userManager.Users
            .OrderBy(u => u.UserName)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }
}
