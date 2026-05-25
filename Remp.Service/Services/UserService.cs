using FluentValidation;
using System.Security.Cryptography;
using Remp.Repository.Interfaces;
using Remp.Models.Entities;
using Remp.Service.DTOs.User;
using Remp.Service.Interfaces;
using AutoMapper;

namespace Remp.Service.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IValidator<CreateAgentRequestDto> _createAgentRequestValidator;
    private readonly IValidator<UpdatePasswordRequestDto> _updatePasswordRequestValidator;
    private readonly IMapper _mapper;

    public UserService(
        IUserRepository userRepository,
        IValidator<CreateAgentRequestDto> createAgentRequestValidator,
        IValidator<UpdatePasswordRequestDto> updatePasswordRequestValidator,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _createAgentRequestValidator = createAgentRequestValidator;
        _updatePasswordRequestValidator = updatePasswordRequestValidator;
        _mapper = mapper;
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

    public async Task<CreateAgentResponseDto> CreateAgentAsync(string currentUserId, CreateAgentRequestDto dto)
    {
        await _createAgentRequestValidator.ValidateAndThrowAsync(dto);

        var company = await _userRepository.GetPhotographyCompanyByIdAsync(currentUserId);
        if (company == null)
        {
            throw new UnauthorizedAccessException("Only photography company accounts can create agent accounts.");
        }

        var temporaryPassword = GenerateTemporaryPassword();
        var userName = dto.Email;

        var user = new User
        {
            UserName = userName,
            Email = dto.Email,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        var agent = new Agent
        {
            AgentFirstName = dto.AgentFirstName,
            AgentLastName = dto.AgentLastName,
            AvatarUrl = dto.AvatarUrl,
            CompanyName = company.PhotographyCompanyName
        };

        var createResult = await _userRepository.CreateAgentAsync(agent, user, temporaryPassword);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(x => x.Description));
            throw new ArgumentException(string.IsNullOrWhiteSpace(errors) ? "Failed to create agent account." : errors);
        }

        var createdAgent = await _userRepository.GetAgentByIdAsync(user.Id);
        if (createdAgent == null)
        {
            throw new InvalidOperationException("Agent account was created, but agent profile was not found.");
        }

        await _userRepository.AddAgentToPhotographyCompany(company, createdAgent);

        return new CreateAgentResponseDto
        {
            AgentId = createdAgent.Id,
            UserName = user.UserName,
            Email = user.Email,
            TemporaryPassword = temporaryPassword
        };
    }

    private static string GenerateTemporaryPassword()
    {
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digits = "0123456789";
        const string special = "!@#$%^&*";
        var allChars = lower + upper + digits + special;

        var passwordChars = new[]
        {
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            special[RandomNumberGenerator.GetInt32(special.Length)]
        }.ToList();

        for (int i = passwordChars.Count; i < 12; i++)
        {
            passwordChars.Add(allChars[RandomNumberGenerator.GetInt32(allChars.Length)]);
        }

        for (int i = passwordChars.Count - 1; i > 0; i--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(i + 1);
            (passwordChars[i], passwordChars[swapIndex]) = (passwordChars[swapIndex], passwordChars[i]);
        }

        return new string(passwordChars.ToArray());
    }

    public async Task<GetAgentResponseDto> GetAgentByEmailAsync(string email)
    {
        var agent = await _userRepository.GetAgentByEmailAsync(email);
        if (agent == null)
        {
            throw new KeyNotFoundException("Agent not found");
        }

        return new GetAgentResponseDto
        {
            Id = agent.Id,
            AgentFirstName = agent.AgentFirstName,
            AgentLastName = agent.AgentLastName,
            Email = agent.User.Email,
            AvatarUrl = agent.AvatarUrl,
            CompanyName = agent.CompanyName
        };
    }

    public async Task<List<GetAgentResponseDto>> GetAgentsByCompanyIdAsync(string companyId)
    {
        var company = await _userRepository.GetPhotographyCompanyByIdAsync(companyId);
        if (company == null)
        {
            throw new KeyNotFoundException("Photography company not found");
        }

        var agents = company.Agents
            .OrderBy(a => a.AgentFirstName)
            .ThenBy(a => a.AgentLastName)
            .ToList();

        return _mapper.Map<List<GetAgentResponseDto>>(agents);
    }

    public async Task UpdatePasswordAsync(string userId, UpdatePasswordRequestDto dto)
    {
        await _updatePasswordRequestValidator.ValidateAndThrowAsync(dto);

        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        var result = await _userRepository.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(x => x.Description));
            throw new ArgumentException(string.IsNullOrWhiteSpace(errors) ? "Failed to update password." : errors);
        }
    }

}
