using System;
using Recam.Service.DTOs;

namespace Recam.Service.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto);
}
