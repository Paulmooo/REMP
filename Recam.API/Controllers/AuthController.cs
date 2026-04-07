using Microsoft.AspNetCore.Mvc;
using Recam.Common.Extensions;
using Recam.Service.DTOs;
using Recam.Service.Interfaces;

namespace Recam.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
        {
            try
            {
                var result = await _authService.RegisterAsync(dto);

                return Ok(ApiResponse<RegisterResponseDto>.Ok(
                    result,
                    "Registration successful."
                ));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<RegisterResponseDto>.Fail(ex.Message));
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            try
            {
                var result = await _authService.LoginAsync(dto);

                return Ok(ApiResponse<LoginResponseDto>.Ok(
                    result,
                    "Login successful."
                ));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<LoginResponseDto>.Fail(ex.Message));
            }
        }
    }
}
