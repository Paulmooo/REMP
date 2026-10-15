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

        /// <summary>Read Listing contacts, ordered by contact ID. An empty Listing returns an empty array.</summary>
        [HttpGet("listings/{id:int}/contacts")]
        [Authorize(Roles = "Admin,Agent")]
        [ProducesResponseType(typeof(ApiResponse<List<CaseContactDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetContacts(int id)
        {
            var userId = User.FindFirstValue("uid") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _listingCaseService.GetContactsAsync(id, userId);
            return Ok(ApiResponse<List<CaseContactDto>>.Ok(result, "Contacts retrieved successfully."));
        }

        /// <summary>Add an independent contact record as an active assigned Agent. Ownership comes from the URL.</summary>
        [HttpPost("listings/{id:int}/contacts")]
        [Authorize(Policy = "AgentPolicy")]
        [ProducesResponseType(typeof(ApiResponse<CaseContactDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddContact(int id, [FromBody] CreateCaseContactRequestDto dto)
        {
            var userId = User.FindFirstValue("uid") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _listingCaseService.AddContactAsync(id, dto, userId);
            return Ok(ApiResponse<CaseContactDto>.Ok(result, "Contact added successfully."));
        }

        /// <summary>Assign an active Agent belonging to the listing owner's company. Idempotent.</summary>
        [HttpPut("listings/{id:int}/agents/{agentId}")]
        [Authorize(Policy = "AdminPolicy")]
        [ProducesResponseType(typeof(ApiResponse<ListingAgentAssignmentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignAgent(int id, string agentId)
        {
            var userId = User.FindFirstValue("uid") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _listingCaseService.AssignAgentAsync(id, agentId, userId);
            return Ok(ApiResponse<ListingAgentAssignmentDto>.Ok(result, "Agent assigned successfully."));
        }

        /// <summary>Remove only this Agent–Listing assignment. An absent assignment succeeds.</summary>
        [HttpDelete("listings/{id:int}/agents/{agentId}")]
        [Authorize(Policy = "AdminPolicy")]
        [ProducesResponseType(typeof(ApiResponse<ListingAgentAssignmentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UnassignAgent(int id, string agentId)
        {
            var userId = User.FindFirstValue("uid") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _listingCaseService.UnassignAgentAsync(id, agentId, userId);
            return Ok(ApiResponse<ListingAgentAssignmentDto>.Ok(result, "Agent unassigned successfully."));
        }

        /// <summary>Read the shared final display pictures and cover for an accessible Listing.</summary>
        [HttpGet("listings/{id:int}/final-selection")]
        [Authorize(Roles = "Admin,Agent")]
        [ProducesResponseType(typeof(ApiResponse<FinalSelectionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFinalSelection(int id)
        {
            var userId = User.FindFirstValue("uid") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _listingCaseService.GetFinalSelectionAsync(id, userId);
            return Ok(ApiResponse<FinalSelectionDto>.Ok(result, "Final selection retrieved successfully."));
        }

        /// <summary>Replace all display pictures (maximum ten) and the optional cover. Delivered Listings are read-only.</summary>
        [HttpPut("listings/{id:int}/selected-media")]
        [Authorize(Policy = "AgentPolicy")]
        [ProducesResponseType(typeof(ApiResponse<FinalSelectionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SaveMediaSelection(int id, [FromBody] SaveMediaSelectionRequestDto dto)
        {
            var userId = User.FindFirstValue("uid") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _listingCaseService.SaveMediaSelectionAsync(id, dto, userId);
            return Ok(ApiResponse<FinalSelectionDto>.Ok(result, "Media selection updated successfully."));
        }

        /// <summary>Set the unique cover to an already selected picture. Delivered Listings are read-only.</summary>
        [HttpPut("listings/{id:int}/cover-image")]
        [Authorize(Policy = "AgentPolicy")]
        [ProducesResponseType(typeof(ApiResponse<FinalSelectionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetHero(int id, [FromBody] SetHeroRequestDto dto)
        {
            var userId = User.FindFirstValue("uid") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _listingCaseService.SetHeroAsync(id, dto, userId);
            return Ok(ApiResponse<FinalSelectionDto>.Ok(result, "Media selection updated successfully."));
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
