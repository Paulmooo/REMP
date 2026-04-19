using System;
using Recam.Repository.Interfaces;
using Recam.Service.DTOs.User;
using Recam.Service.Interfaces;

namespace Recam.Service.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserInfoDto> FindCurrentUserInfoAsync(string userId)
    {
        var currentUser = await _userRepository.GetUserByIdAsync(userId);
        if (currentUser == null)
        {
            throw new ArgumentException("User not found");
        }

        var roles = await _userRepository.GetRolesByUserIdAsync(userId);
        if (roles.Count == 0)
        {
            throw new ArgumentException("User role not found");
        }

        List<int> listingCaseIds;
        if (roles.Contains("Admin"))
        {
            listingCaseIds = (await _userRepository.GetListingCasesByUserIdAdminAsync(userId))
                .Select(lc => lc.Id)
                .ToList();
        }
        else if (roles.Contains("Agent"))
        {
            listingCaseIds = (await _userRepository.GetListingCasesByUserIdAgentAsync(userId))
                .Select(lc => lc.Id)
                .ToList();
        }
        else
        {
            throw new ArgumentException("Unsupported role");
        }

        return new UserInfoDto
        {
            Id = userId,
            Roles = roles,
            ListingCaseIds = listingCaseIds
        };
    }

    public async Task AddAgentToPhotographyCompanyAsync(string userId, string companyId)
    {
        var agent = await _userRepository.GetAgentByIdAsync(userId);
        if (agent == null)
        {
            throw new KeyNotFoundException("Agent not found");
        }

        var company = await _userRepository.GetPhotographyCompanyByIdAsync(companyId);
        if (company == null)
        {
            throw new KeyNotFoundException("Photography company not found");
        }

        if (company.Agents.Any(a => a.Id == userId))
        {
            throw new InvalidOperationException("Agent is already part of the photography company");
        }

        await _userRepository.AddAgentToPhotographyCompany(company, agent);

    }
}
