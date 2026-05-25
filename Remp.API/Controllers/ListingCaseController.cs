using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remp.Common.Extensions;
using Remp.Models.Enums;
using Remp.Service.DTOs.ListingCase;
using Remp.Service.Interfaces;
using System.Security.Claims;

namespace Remp.API.Controllers
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
            var roles = User.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .Distinct()
                .ToList();
            var role = roles.FirstOrDefault();
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

        [HttpGet("listings/{id}")]
        [Authorize]
        public async Task<IActionResult> GetListingCaseDetails(int id)
        {
            var userId = User.FindFirstValue("uid")
                ?? User.Claims.LastOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User ID claim is missing in token.");
            }

            var roles = User.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .Distinct()
                .ToList();
            var role = roles.FirstOrDefault();
            var listingCase = await _listingCaseService.GetListingCaseDetailsAsync(id, userId, role);
            return Ok(ApiResponse<ListingCaseItemDto>.Ok(listingCase, "Listing case details retrieved successfully."));
        }

        [HttpPatch("listings/{id}/status")]
        [Authorize]
        public async Task<IActionResult> ChangeListingCaseStatus(int id, [FromQuery] ListcaseStatus newStatus)
        {
            if (!Enum.IsDefined(typeof(ListcaseStatus), newStatus))
            {
                return BadRequest(ApiResponse<object>.Fail("Invalid status value."));
            }
            
            var userId = User.FindFirstValue("uid")
                ?? User.Claims.LastOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User ID claim is missing in token.");
            }

            await _listingCaseService.ChangeListingCaseStatusAsync(id, newStatus, userId);
            return Ok(ApiResponse<object>.Ok(new { Id = id, NewStatus = newStatus }, "Listing case status updated successfully."));
        }

        [HttpGet("listings/{id}/media")]
        [Authorize]
        public async Task<IActionResult> GetListingCaseMediaAsset(int id)
        {
            var userId = User.FindFirstValue("uid")
                ?? User.Claims.LastOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User ID claim is missing in token.");
            }

            var roles = User.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .Distinct()
                .ToList();
            var role = roles.FirstOrDefault();
            var mediaAssets = await _listingCaseService.GetListingCaseMediaAssetsAsync(id, userId, role);
            return Ok(ApiResponse<List<MediaAssetGroupDto>>.Ok(mediaAssets, "Listing case media assets retrieved successfully."));
        }
    }
}
