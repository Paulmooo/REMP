using Microsoft.AspNetCore.Identity;
using Remp.Models.Entities;

namespace Remp.Repository.Interfaces;

public interface IAuthRepository
{
    Task<User?> FindByUsernameAsync(string username);
    Task<IdentityResult> RegisterWithRoleAsync(User user, string password, string role);
    Task<List<string>> GetRolesAsync(User user);
    Task<bool> CheckPasswordAsync(User user, string password);
    Task<int> GetUserCountAsync();
    Task<List<User>> GetUsersPagedAsync(int skip, int take);
}
