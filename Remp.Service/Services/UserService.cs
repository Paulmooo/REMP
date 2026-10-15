using Remp.Common.Exceptions;
using FluentValidation;
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
        if (currentUser == null || currentUser.IsDeleted)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        var roles = await _userRepository.GetRolesByUserIdAsync(userId);
        if (roles.Count == 0)
        {
            throw new ForbiddenException("User role not found");
        }

        if (roles.Contains("Agent") && await _userRepository.GetAgentByIdAsync(userId) == null ||
            roles.Contains("Admin") && await _userRepository.GetPhotographyCompanyByIdAsync(userId) == null)
            throw new ForbiddenException("Account roles and profiles are inconsistent.");

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
            throw new ForbiddenException("Account role is not supported by this workflow.");
        }

        return new UserInfoDto
        {
            Id = userId,
            Roles = roles,
            ListingCaseIds = listingCaseIds
        };
    }

    public async Task AddAgentToPhotographyCompanyAsync(string currentUserId, string agentId, string companyId)
    {
        if (string.IsNullOrWhiteSpace(currentUserId))
            throw new UnauthorizedAccessException("User ID claim is missing in token.");

        if (companyId != currentUserId)
            throw new ForbiddenException("Users can only manage their own photography company.");

        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("Agent ID is required.");

        var company = await _userRepository.GetPhotographyCompanyByIdAsync(companyId)
            ?? throw new KeyNotFoundException("Photography company not found");
        var agent = await _userRepository.GetAgentByIdAsync(agentId)
            ?? throw new KeyNotFoundException("Agent not found");

        if (company.Agents.Any(a => a.Id == agentId))
            throw new ConflictException("Agent is already part of the photography company");

        await _userRepository.AddAgentToPhotographyCompany(company, agent);
    }

    public async Task<CreateAgentResponseDto> CreateAgentAsync(string currentUserId, CreateAgentRequestDto dto)
    {
        await _createAgentRequestValidator.ValidateAndThrowAsync(dto);

        var company = await _userRepository.GetPhotographyCompanyByIdAsync(currentUserId);
        if (company == null)
        {
            throw new ForbiddenException("Only photography company accounts can create agent accounts.");
        }

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
            AvatarUrl = dto.AvatarUrl ?? string.Empty,
            CompanyName = company.PhotographyCompanyName
        };

        var createResult = await _userRepository.CreateAgentAsync(agent, user, null, company);
        if (!createResult.Succeeded)
        {
            if (createResult.Errors.Any(x => x.Code is "DuplicateUserName" or "DuplicateEmail"))
                throw new ConflictException("An account with this email or user name already exists.");
            throw new ArgumentException("Failed to create agent account. Check the account input and password policy.");
        }

        return new CreateAgentResponseDto
        {
            AgentId = agent.Id,
            UserName = user.UserName,
            Email = user.Email,
            RequiresInvitation = true
        };
    }

    public async Task<GetAgentResponseDto> GetAgentByEmailAsync(string currentUserId, string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            throw new ArgumentException("A valid exact email is required.");
        if (string.IsNullOrWhiteSpace(currentUserId))
            throw new UnauthorizedAccessException("User ID claim is missing in token.");
        if (await _userRepository.GetPhotographyCompanyByIdAsync(currentUserId) == null)
            throw new ForbiddenException("Only photography company accounts can search agents.");
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

        var agents = await _userRepository.GetAgentsByCompanyIdAsync(companyId);

        return _mapper.Map<List<GetAgentResponseDto>>(agents);
    }

    public async Task UpdatePasswordAsync(string userId, UpdatePasswordRequestDto dto)
    {
        await _updatePasswordRequestValidator.ValidateAndThrowAsync(dto);

        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        var result = await _userRepository.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
        {
            throw new ArgumentException("Failed to update password. Check the current password and new password policy.");
        }
    }

}
