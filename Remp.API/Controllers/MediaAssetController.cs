using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Remp.Common.Extensions;
using Remp.Models.Enums;
using Remp.Service.DTOs.ListingCase;
using Remp.Service.Interfaces;

namespace Remp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MediaAssetController : ControllerBase
    {
        private readonly IMediaAssetService _mediaAssetService;

        public MediaAssetController(IMediaAssetService mediaAssetService)
        {
            _mediaAssetService = mediaAssetService;
        }

        [HttpPost("media/upload")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> UploadMediaAssets(
            [FromForm] List<IFormFile> files,
            [FromForm] MediaType type,
            [FromForm] int listingCaseId
            )
        {
            var userId = User.FindFirstValue("uid")
                ?? User.Claims.LastOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User ID claim is missing in token.");
            }

            var mediaAssets = await _mediaAssetService.UploadMediaAssetsAsync(files, type, listingCaseId, userId);

            return Ok(ApiResponse<List<MediaAssetDto>>.Ok(mediaAssets, "Media assets uploaded successfully."));
        }

        [HttpDelete("media/{id}")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> DeleteMediaAsset(int id)
        {
            var userId = User.FindFirstValue("uid")
                ?? User.Claims.LastOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User ID claim is missing in token.");
            }

            await _mediaAssetService.DeleteMediaAssetAsync(id, userId);

            return Ok(ApiResponse<object>.Ok(new { Id = id }, "Media asset deleted successfully."));
        }
    }
}
