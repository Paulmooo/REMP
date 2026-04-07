using Microsoft.AspNetCore.Identity;
using Recam.Models.Entities;

namespace Recam.Repository.Interfaces;

public interface IAuthRepository
{
    Task<User?> FindByUsernameAsync(string username);
    Task<IdentityResult> RegisterWithRoleAsync(User user, string password, string role);
    Task<List<string>> GetRolesAsync(User user);
    Task<bool> CheckPasswordAsync(User user, string password);
}
