using System;

using Recam.Service.DTOs.User;

namespace Recam.Service.Interfaces;

public interface IUserService
{
    Task<UserInfoDto> FindCurrentUserInfoAsync(string userId);
    Task AddAgentToPhotographyCompanyAsync(string userId, string companyId);
    Task<CreateAgentResponseDto> CreateAgentAsync(string currentUserId, CreateAgentRequestDto dto);
    Task<GetAgentResponseDto> GetAgentByEmailAsync(string email);
}
