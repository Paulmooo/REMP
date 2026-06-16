using System;
using Microsoft.AspNetCore.Identity;
using Remp.Models.Entities;

namespace Remp.Repository.Interfaces;

public interface IUserRepository
{
    Task<User?> GetUserByIdAsync(string userId);
    Task<List<string>> GetRolesByUserIdAsync(string userId);
    Task<List<ListingCase>> GetListingCasesByUserIdAdminAsync(string userId);
    Task<List<ListingCase>> GetListingCasesByUserIdAgentAsync(string userId);
    Task<Agent?> GetAgentByIdAsync(string userId);
    Task<PhotographyCompany?> GetPhotographyCompanyByIdAsync(string companyId);
    Task AddAgentToPhotographyCompany(PhotographyCompany company, Agent agent);
    Task<IdentityResult> CreateAgentAsync(Agent agent, User user, string password);
    Task<Agent?> GetAgentByEmailAsync(string email);
    Task<IdentityResult> ChangePasswordAsync(User user, string currentPassword, string newPassword);
}
