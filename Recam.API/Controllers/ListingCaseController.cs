using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recam.Common.Extensions;
using Recam.Service.DTOs;
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
            try
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
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<CreateListingCaseResponseDto>.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse<CreateListingCaseResponseDto>.Fail(ex.Message));
            }
        }

        [HttpPut("listings/{id}")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> UpdateListingCase(int id, [FromBody] UpdateListingCaseRequestDto dto)
        {
            try
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
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse<object>.Fail(ex.Message));
            }
        }
    }
}
