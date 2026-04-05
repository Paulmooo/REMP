using Microsoft.AspNetCore.Identity;
using Recam.Models.Entities;

namespace Recam.Repository.Interfaces;

public interface IAuthRepository
{
    Task<User?> FindByUsernameAsync(string username);
    Task<IdentityResult> RegisterWithRoleAsync(User user, string password, string role);
    Task<IList<string>> GetRolesAsync(User user);
}
