using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Recam.Common.Extensions;
using Recam.Models.Entities;
using Recam.Service.DTOs;
using Recam.Service.Interfaces;

namespace Recam.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<User> _userManager;
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
    }
}
