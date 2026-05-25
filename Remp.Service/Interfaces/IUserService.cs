using System;
using Remp.Models.Entities;
using Remp.Service.DTOs.User;

namespace Remp.Service.Interfaces;

public interface IUserService
{
    Task<UserInfoDto> FindCurrentUserInfoAsync(string userId);
    Task AddAgentToPhotographyCompanyAsync(string userId, string companyId);
    Task<CreateAgentResponseDto> CreateAgentAsync(string currentUserId, CreateAgentRequestDto dto);
    Task<GetAgentResponseDto> GetAgentByEmailAsync(string email);
    Task<List<GetAgentResponseDto>> GetAgentsByCompanyIdAsync(string companyId);
    Task UpdatePasswordAsync(string userId, UpdatePasswordRequestDto dto);
}
