using System;
using Recam.Service.DTOs;

namespace Recam.Service.Interfaces;

public interface IAuthService
{
    Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto dto);
    Task<LoginResponseDto> LoginAsync(LoginRequestDto dto);
    Task<PagedUsersResponseDto> GetAllUsersAsync(int page, int pageSize);
}