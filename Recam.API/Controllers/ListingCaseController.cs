using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recam.Common.Extensions;
using Recam.Service.DTOs.ListingCase;
using Recam.Service.Interfaces;
using System.Security.Claims;

namespace Recam.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ListingCaseController : ControllerBase
    {
        private readonly IListingCaseService _listingCaseService;

        public ListingCaseController(IListingCaseService listingCaseService)
        {
            _listingCaseService = listingCaseService;
        }

        [HttpPost("listings")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> CreateListingCase([FromBody] CreateListingCaseRequestDto dto)
        {
            var userId = User.FindFirstValue("uid")
                ?? User.Claims.LastOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User ID claim is missing in token.");
            }

            var responseDto = await _listingCaseService.CreateListingCaseAsync(dto, userId);

            return Ok(ApiResponse<CreateListingCaseResponseDto>.Ok(
                responseDto,
                "Listing case created successfully."
            ));
        }

        [HttpPut("listings/{id}")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> UpdateListingCase(int id, [FromBody] UpdateListingCaseRequestDto dto)
        {
            var userId = User.FindFirstValue("uid")
                ?? User.Claims.LastOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User ID claim is missing in token.");
            }

            await _listingCaseService.UpdateListingCaseAsync(id, dto, userId);

            return Ok(ApiResponse<object>.Ok(new { Id = id }, "Listing case updated successfully."));
        }

        [HttpGet("listings")]
        [Authorize]
        public async Task<IActionResult> GetAllListingCases([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var userId = User.FindFirstValue("uid")
                ?? User.Claims.LastOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User ID claim is missing in token.");
            }
            var role = User.FindFirstValue(ClaimTypes.Role);
            var listingCases = await _listingCaseService.GetAllListingCasesAsync(pageNumber, pageSize, userId, role);
            return Ok(ApiResponse<PagedListingCasesResponseDto>.Ok(listingCases, "Listing cases retrieved successfully."));
        }

        [HttpDelete("listings/{id}")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> DeleteListingCase(int id)
        {
            var userId = User.FindFirstValue("uid")
                ?? User.Claims.LastOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User ID claim is missing in token.");
            }

            await _listingCaseService.DeleteListingCaseAsync(id, userId);
            return Ok(ApiResponse<object>.Ok(new { Id = id }, "Listing case deleted successfully."));
        }
    }
}
