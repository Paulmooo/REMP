using System;
using Recam.Models.Entities;

namespace Recam.Repository.Interfaces;

public interface IUserRepository
{
    Task<User?> GetUserByIdAsync(string userId);
    Task<List<string>> GetRolesByUserIdAsync(string userId);
    Task<List<ListingCase>> GetListingCasesByUserIdAdminAsync(string userId);
    Task<List<ListingCase>> GetListingCasesByUserIdAgentAsync(string userId);
}
