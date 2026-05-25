using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remp.Common.Extensions;
using Remp.Service.DTOs.User;
using Remp.Service.Interfaces;

namespace Remp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUserInfo()
        {
            var userId = User.FindFirstValue("uid")
                ?? User.Claims.LastOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User ID claim is missing in token.");
            }

            var userInfo = await _userService.FindCurrentUserInfoAsync(userId);

            return Ok(ApiResponse<UserInfoDto>.Ok(
                userInfo,
                "User info retrieved successfully."
            ));
        }

        [HttpPost("photographycompany/{companyId}/agent")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> AddAgentToPhotographyCompany(string companyId, [FromBody] AddAgentToPhotographyCompanyRequestDto dto)
        {
            await _userService.AddAgentToPhotographyCompanyAsync(dto.AgentId, companyId);
            return Ok(ApiResponse<string>.Ok(null, "Agent added to photography company successfully."));
        }

        [HttpPost("agent")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> CreateAgent([FromBody] CreateAgentRequestDto dto)
        {
            var userId = User.FindFirstValue("uid")
                ?? User.Claims.LastOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User ID claim is missing in token.");
            }

            var result = await _userService.CreateAgentAsync(userId, dto);

            return Ok(ApiResponse<CreateAgentResponseDto>.Ok(
                result,
                "Agent account created successfully."
            ));
        }

        [HttpGet("agent/{email}")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> GetAgentByEmail(string email)
        {
            var agent = await _userService.GetAgentByEmailAsync(email);
            if (agent == null)
            {
                return NotFound(ApiResponse<string>.Fail("Agent not found."));
            }

            return Ok(ApiResponse<GetAgentResponseDto>.Ok(
                agent,
                "Agent retrieved successfully."
            ));
        }

        [HttpGet("agents")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> GetCompanyAgentList()
        {
            var userId = User.FindFirstValue("uid")
                ?? User.Claims.LastOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User ID claim is missing in token.");
            }

            var agents = await _userService.GetAgentsByCompanyIdAsync(userId);
            return Ok(ApiResponse<List<GetAgentResponseDto>>.Ok(
                agents,
                "Agents retrieved successfully."
            ));
        }

        [HttpPut("password")]
        [Authorize]
        public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequestDto dto)
        {
            var userId = User.FindFirstValue("uid")
                ?? User.Claims.LastOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User ID claim is missing in token.");
            }

            await _userService.UpdatePasswordAsync(userId, dto);

            return Ok(ApiResponse<string>.Ok(null, "Password updated successfully."));
        }
    }
}
