using Remp.Service.DTOs.Auth;

namespace Remp.Service.Interfaces;

public interface IAuthService
{
    Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto dto);
    Task<LoginResponseDto> LoginAsync(LoginRequestDto dto);
    Task<PagedUsersResponseDto> GetAllUsersAsync(int page, int pageSize);
}