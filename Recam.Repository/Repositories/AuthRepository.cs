using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Recam.DataAccess.Data;
using Recam.Models.Entities;
using Recam.Repository.Interfaces;

namespace Recam.Repository.Repositories;

public class AuthRepository : IAuthRepository
{
    private readonly RecamDbContext _dbContext;
    private readonly UserManager<User> _userManager;

    public AuthRepository(RecamDbContext dbContext, UserManager<User> userManager)
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
}
