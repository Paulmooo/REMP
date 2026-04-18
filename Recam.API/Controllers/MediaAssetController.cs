using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Recam.Common.Extensions;
using Recam.Service.Interfaces;

namespace Recam.API.Controllers
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
