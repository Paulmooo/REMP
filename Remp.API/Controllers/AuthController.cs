using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remp.Common.Extensions;
using Remp.Service.DTOs.Auth;
using Remp.Service.Interfaces;

namespace Remp.API.Controllers
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

        [HttpGet("users")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> GetAllUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var users = await _authService.GetAllUsersAsync(pageNumber, pageSize);
            return Ok(ApiResponse<PagedUsersResponseDto>.Ok(users, "Users retrieved successfully."));
        }
    }
}
