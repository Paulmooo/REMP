using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Recam.Common.Extensions;
using Recam.Service.DTOs.User;
using Recam.Service.Interfaces;

namespace Recam.API.Controllers
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
    }
}
