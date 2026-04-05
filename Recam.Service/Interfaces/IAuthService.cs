using System;
using Recam.Service.DTOs;

namespace Recam.Service.Interfaces;

public interface IAuthService
{
    Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto dto);
}
