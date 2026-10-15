using Remp.Common.Exceptions;
using AutoMapper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Remp.Models.Entities;
using Remp.Models.Enums;
using Remp.Repository.Interfaces;
using Remp.Service.DTOs.ListingCase;
using Remp.Service.Interfaces;

namespace Remp.Service.Services;

public class ListingCaseService : IListingCaseService
{
    private readonly IListingCaseRepository _listingCaseRepository;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateListingCaseRequestDto> _createValidator;
    private readonly IValidator<UpdateListingCaseRequestDto> _updateValidator;

    public ListingCaseService(
        IListingCaseRepository listingCaseRepository,
        IMapper mapper,
        IValidator<CreateListingCaseRequestDto> createValidator,
        IValidator<UpdateListingCaseRequestDto> updateValidator)
    {
        _listingCaseRepository = listingCaseRepository;
        _mapper = mapper;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<List<CaseContactDto>> GetContactsAsync(int id, string userId)
    {
        ValidateContactIdentity(id, userId);
        var listing = await _listingCaseRepository.GetListingCaseByIdAsync(id)
            ?? throw new KeyNotFoundException("Listing case not found.");
        await AuthorizeContactsAsync(listing, userId, false);
        return _mapper.Map<List<CaseContactDto>>(await _listingCaseRepository.GetContactsAsync(id));
    }

    public async Task<CaseContactDto> AddContactAsync(int id, CreateCaseContactRequestDto dto, string userId)
    {
        ValidateContactIdentity(id, userId);
        if (dto == null) throw new ArgumentException("A contact request is required.");
        var normalized = new CreateCaseContactRequestDto
        {
            FirstName = dto.FirstName?.Trim() ?? "", LastName = dto.LastName?.Trim() ?? "",
            CompanyName = dto.CompanyName?.Trim() ?? "", ProfileUrl = dto.ProfileUrl?.Trim() ?? "",
            Email = dto.Email?.Trim() ?? "", PhoneNumber = dto.PhoneNumber?.Trim() ?? ""
        };
        var validation = await new Remp.Service.Validators.CreateCaseContactRequestValidator().ValidateAsync(normalized);
        if (!validation.IsValid) throw new ArgumentException(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        var contact = new CaseContact
        {
            FirstName = normalized.FirstName, LastName = normalized.LastName,
            CompanyName = normalized.CompanyName!, ProfileUrl = normalized.ProfileUrl!,
            Email = normalized.Email!, PhoneNumber = normalized.PhoneNumber!
        };
        var result = await _listingCaseRepository.AddContactAsync(id, contact,
            listing => AuthorizeContactsAsync(listing, userId, true));
        return _mapper.Map<CaseContactDto>(result);
    }

    private static void ValidateContactIdentity(int id, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("User ID claim is missing in token.");
        if (id <= 0) throw new ArgumentException("A positive Listing ID is required.");
    }

    private async Task AuthorizeContactsAsync(ListingCase listing, string userId, bool write)
    {
        if (!write && listing.UserId == userId && await _listingCaseRepository.IsActiveCompanyAdminAsync(userId)) return;
        var agent = await _listingCaseRepository.GetActiveAgentAsync(userId);
        if (agent == null || !listing.Agents.Any(a => a.Id == userId) ||
            !agent.PhotographyCompanies.Any(c => c.Id == listing.UserId))
            throw new ForbiddenException("An active assigned Agent from the listing company is required.");
    }

    public Task<ListingAgentAssignmentDto> AssignAgentAsync(int id, string agentId, string userId) =>
        SetAssignmentAsync(id, agentId, userId, true);

    public async Task<FinalSelectionDto> GetFinalSelectionAsync(int id, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("User ID claim is missing in token.");
        if (id <= 0) throw new ArgumentException("A positive Listing ID is required.");
        var admin = await _listingCaseRepository.IsActiveCompanyAdminAsync(userId);
        var agent = await _listingCaseRepository.GetActiveAgentAsync(userId);
        if (!admin && agent == null) throw new ForbiddenException("An active company administrator or Agent is required.");
        var listing = await _listingCaseRepository.GetListingCaseDetailsByIdAsync(id)
            ?? throw new KeyNotFoundException("Listing case not found.");
        var assigned = agent != null && listing.Agents.Any(a => a.Id == userId) &&
            agent.PhotographyCompanies.Any(c => c.Id == listing.UserId);
        if (!(admin && listing.UserId == userId) && !assigned)
            throw new ForbiddenException("The listing is not accessible to this user.");
        return MapFinalSelection(listing);
    }

    public Task<FinalSelectionDto> SaveMediaSelectionAsync(int id, SaveMediaSelectionRequestDto dto, string userId)
    {
        if (dto?.MediaIds == null) throw new ArgumentException("Media IDs are required; use an empty array to clear selection.");
        if (dto.MediaIds.Count > 10 || dto.MediaIds.Any(i => i <= 0) || dto.MediaIds.Distinct().Count() != dto.MediaIds.Count)
            throw new ArgumentException("Select at most ten distinct positive media IDs.");
        if (dto.HeroMediaId.HasValue && !dto.MediaIds.Contains(dto.HeroMediaId.Value))
            throw new ArgumentException("The cover must be included in the display pictures.");
        return UpdateSelectionAsync(id, userId, dto.MediaIds.ToArray(), dto.HeroMediaId);
    }

    public Task<FinalSelectionDto> SetHeroAsync(int id, SetHeroRequestDto dto, string userId)
    {
        if (dto?.MediaId == null || dto.MediaId <= 0) throw new ArgumentException("A positive cover media ID is required.");
        return UpdateSelectionAsync(id, userId, null, dto.MediaId);
    }

    private async Task<FinalSelectionDto> UpdateSelectionAsync(int id, string userId, int[]? mediaIds, int? heroId)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("User ID claim is missing in token.");
        if (id <= 0) throw new ArgumentException("A positive Listing ID is required.");
        if (await _listingCaseRepository.GetActiveAgentAsync(userId) == null)
            throw new ForbiddenException("Only an active assigned Agent can modify media selection.");
        var result = await _listingCaseRepository.UpdateMediaSelectionAsync(id, async listing =>
        {
            var agent = await _listingCaseRepository.GetActiveAgentAsync(userId);
            if (agent == null || !listing.Agents.Any(a => a.Id == userId) ||
                !agent.PhotographyCompanies.Any(c => c.Id == listing.UserId))
                throw new ForbiddenException("Only an active assigned Agent from the listing company can modify selection.");
            if (listing.ListingStatus == ListcaseStatus.Delivered)
                throw new ArgumentException("Delivered listing media selection cannot be modified.");
            var selected = mediaIds ?? listing.MediaAssets.Where(m => m.IsSelected).Select(m => m.Id).ToArray();
            var valid = listing.MediaAssets.Where(m => !m.IsDeleted && m.MediaType == MediaType.Picture).Select(m => m.Id).ToHashSet();
            if (selected.Length > 10 || selected.Any(i => !valid.Contains(i)))
                throw new ArgumentException("All display media must be undeleted pictures belonging to this Listing (maximum ten).");
            if (heroId.HasValue && (!valid.Contains(heroId.Value) || !selected.Contains(heroId.Value)))
                throw new ArgumentException("The cover must be one of the selected display pictures.");
            foreach (var media in listing.MediaAssets)
            {
                media.IsSelected = selected.Contains(media.Id);
                media.IsHero = media.Id == heroId;
            }
        });
        return MapFinalSelection(result);
    }

    private FinalSelectionDto MapFinalSelection(ListingCase listing)
    {
        var pictures = listing.MediaAssets.Where(m => !m.IsDeleted && m.MediaType == MediaType.Picture)
            .OrderBy(m => m.Id).ToList();
        return new FinalSelectionDto
        {
            ListingCaseId = listing.Id,
            Pictures = _mapper.Map<List<MediaAssetDto>>(pictures.Where(m => m.IsSelected).ToList()),
            Hero = _mapper.Map<MediaAssetDto>(pictures.FirstOrDefault(m => m.IsHero))
        };
    }

    public Task<ListingAgentAssignmentDto> UnassignAgentAsync(int id, string agentId, string userId) =>
        SetAssignmentAsync(id, agentId, userId, false);

    private async Task<ListingAgentAssignmentDto> SetAssignmentAsync(int id, string agentId, string userId, bool assigned)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("User ID claim is missing in token.");
        if (id <= 0 || string.IsNullOrWhiteSpace(agentId)) throw new ArgumentException("Listing ID and Agent ID are required.");
        if (!await _listingCaseRepository.IsActiveCompanyAdminAsync(userId))
            throw new ForbiddenException("Only an active photography company administrator can manage assignments.");
        var listing = await _listingCaseRepository.GetListingCaseByIdAsync(id)
            ?? throw new KeyNotFoundException("Listing case not found.");
        if (listing.UserId != userId) throw new ForbiddenException("Only the listing owner can manage assignments.");
        if (assigned)
        {
            var agent = await _listingCaseRepository.GetActiveAgentAsync(agentId)
                ?? throw new KeyNotFoundException("Active Agent not found.");
            if (!agent.PhotographyCompanies.Any(c => c.Id == userId))
                throw new ForbiddenException("Agent must belong to the listing's photography company.");
        }
        // Removal also cleans up stale assignments after an Agent is deleted or leaves a company.
        await _listingCaseRepository.SetAgentAssignmentAsync(id, agentId, assigned);
        return new ListingAgentAssignmentDto { ListingCaseId = id, AgentId = agentId, Assigned = assigned };
    }

    public async Task<CreateListingCaseResponseDto> CreateListingCaseAsync(CreateListingCaseRequestDto dto, string userId)
    {
        var validationResult = await _createValidator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new ArgumentException(errors);
        }

        var userExists = await _listingCaseRepository.UserExistsAsync(userId);
        if (!userExists)
        {
            throw new UnauthorizedAccessException("The user in the JWT token does not exist.");
        }

        var listingCase = _mapper.Map<ListingCase>(dto);
        listingCase.CreatedAt = DateTime.UtcNow;
        listingCase.IsDeleted = false;
        listingCase.UserId = userId;
        listingCase.ListingStatus = ListcaseStatus.Created;

        int newId = await _listingCaseRepository.CreateListingCaseAsync(listingCase);

        return new CreateListingCaseResponseDto
        {
            Id = newId,
            CreatedAt = listingCase.CreatedAt
        };
    }

    public async Task UpdateListingCaseAsync(int id, UpdateListingCaseRequestDto dto, string userId)
    {
        var validationResult = await _updateValidator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new ArgumentException(errors);
        }

        var userExists = await _listingCaseRepository.UserExistsAsync(userId);
        if (!userExists)
        {
            throw new UnauthorizedAccessException("The user in the JWT token does not exist.");
        }

        var existingCase = await _listingCaseRepository.GetListingCaseByIdAsync(id);
        if (existingCase == null)
        {
            throw new KeyNotFoundException($"Listing case with ID {id} not found.");
        }

        if (existingCase.UserId != userId)
        {
            throw new ForbiddenException("Users can only update their own listing cases.");
        }

        if (existingCase.IsDeleted)
        {
            throw new ArgumentException("Deleted listing cases cannot be updated.");
        }

        if (existingCase.ListingStatus == ListcaseStatus.Delivered)
        {
            throw new ArgumentException("Listing case is delivered and cannot be updated.");
        }

        _mapper.Map(dto, existingCase);

        await _listingCaseRepository.UpdateListingCaseAsync(existingCase);
    }

    public async Task<PagedListingCasesResponseDto> GetAllListingCasesAsync(int pageNumber, int pageSize, string userId, string role)
    {
        var userExists = await _listingCaseRepository.UserExistsAsync(userId);
        if (!userExists)
        {
            throw new UnauthorizedAccessException("The user in the JWT token does not exist.");
        }

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        int totalCount;
        List<ListingCase> currentPagedCases;

        if (role == "Admin")
        {
            totalCount = await _listingCaseRepository.GetListingCaseCountAsync(userId);
            currentPagedCases = await _listingCaseRepository.GetListingCasesPagedAsync((pageNumber - 1) * pageSize, pageSize, userId);
        }
        else if (role == "Agent")
        {
            totalCount = await _listingCaseRepository.GetListingCaseAssignedToAgentCountAsync(userId);
            currentPagedCases = await _listingCaseRepository.GetListingCasePagedAssignedToAgentAsync((pageNumber - 1) * pageSize, pageSize, userId);
        }
        else throw new UnauthorizedAccessException("Unsupported role");
        
        return new PagedListingCasesResponseDto
            {
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                ListingCases = _mapper.Map<List<ListingCaseItemDto>>(currentPagedCases)
            };
    }

    public async Task DeleteListingCaseAsync(int id, string userId)
    {
        var userExists = await _listingCaseRepository.UserExistsAsync(userId);
        if (!userExists)        
        {
            throw new UnauthorizedAccessException("The user in the JWT token does not exist.");
        }

        var existingCase = await _listingCaseRepository.GetListingCaseByIdAsync(id);
        if (existingCase == null)
        {
            throw new KeyNotFoundException($"Listing case with ID {id} not found.");
        }

        if (existingCase.UserId != userId)
        {
            throw new UnauthorizedAccessException("Users can only delete their own listing cases.");
        }

        await _listingCaseRepository.DeleteListingCaseAsync(id);
    }

    public async Task<ListingCaseItemDto> GetListingCaseDetailsAsync(int listingCaseId, string userId, string role)
    {
        var userExists = await _listingCaseRepository.UserExistsAsync(userId);
        if (!userExists)
        {
            throw new UnauthorizedAccessException("The user in the JWT token does not exist.");
        }

        var listingCase = await _listingCaseRepository.GetListingCaseDetailsByIdAsync(listingCaseId);
        if (listingCase == null || listingCase.IsDeleted)
        {
            throw new KeyNotFoundException($"Listing case with ID {listingCaseId} not found.");
        }

        if (role == "Admin")
        {
            if (listingCase.UserId != userId)
                throw new UnauthorizedAccessException("Admins can only access their own listing cases.");
        }
        else if (role == "Agent")
        {
            if (listingCase.Agents?.Any(a => a.Id == userId) != true)
                throw new UnauthorizedAccessException("Agents can only access listing cases assigned to them.");
        }
        else
        {
            throw new UnauthorizedAccessException("Unsupported role");
        }
        await AuthorizeContactsAsync(listingCase, userId, false);
        return _mapper.Map<ListingCaseItemDto>(listingCase);
    }


    public async Task ChangeListingCaseStatusAsync(int id, ListcaseStatus newStatus, string userId)
    {
        var userExists = await _listingCaseRepository.UserExistsAsync(userId);
        if (!userExists)
        {
            throw new UnauthorizedAccessException("The user in the JWT token does not exist.");
        }

        var existingCase = await _listingCaseRepository.GetListingCaseByIdAsync(id);
        if (existingCase == null)
        {
            throw new KeyNotFoundException($"Listing case with ID {id} not found.");
        }

        if (existingCase.IsDeleted)
        {
            throw new ArgumentException("Deleted listing cases cannot be updated.");
        }

        var isOwner = existingCase.UserId == userId;
        var isAssignedAgent = existingCase.Agents?.Any(a => a.Id == userId) == true;

        if (!isOwner && !isAssignedAgent)
            throw new UnauthorizedAccessException("Only owner or assigned agent can update status.");

        if (!Enum.IsDefined(typeof(ListcaseStatus), newStatus))
            throw new ArgumentException("Invalid status value.");

        var current = existingCase.ListingStatus;

        if (!IsValidTransition(current, newStatus))
            throw new ArgumentException($"Invalid status transition: {current} -> {newStatus}.");

        existingCase.ListingStatus = newStatus;
        await _listingCaseRepository.UpdateListingCaseAsync(existingCase);
    }

    // helper method to validate allowed status transitions
    private static bool IsValidTransition(ListcaseStatus current, ListcaseStatus next)
    {
        return (current, next) switch
        {
            (ListcaseStatus.Created, ListcaseStatus.Pending) => true,
            (ListcaseStatus.Pending, ListcaseStatus.Delivered) => true,
            _ => false
        };
    }

    public async Task<List<MediaAssetGroupDto>> GetListingCaseMediaAssetsAsync(int listingCaseId, string userId, string role)
    {
        var userExists = await _listingCaseRepository.UserExistsAsync(userId);
        if (!userExists)
        {
            throw new UnauthorizedAccessException("The user in the JWT token does not exist.");
        }

        var listingCase = await _listingCaseRepository.GetListingCaseDetailsByIdAsync(listingCaseId);
        if (listingCase == null || listingCase.IsDeleted)
        {
            throw new KeyNotFoundException($"Listing case with ID {listingCaseId} not found.");
        }

        if (role == "Admin")
        {
            if (listingCase.UserId != userId)
                throw new UnauthorizedAccessException("Admins can only access their own listing cases.");
        }
        else if (role == "Agent")
        {
            if (listingCase.Agents?.Any(a => a.Id == userId) != true)
                throw new UnauthorizedAccessException("Agents can only access listing cases assigned to them.");
        }
        else
        {
            throw new UnauthorizedAccessException("Unsupported role");
        }

        var mediaDtos = _mapper.Map<List<MediaAssetDto>>(listingCase.MediaAssets ?? new List<MediaAsset>());

        return mediaDtos
            .GroupBy(x => x.MediaType)
            .Select(g => new MediaAssetGroupDto
            {
                MediaType = g.Key,
                Items = g.OrderByDescending(x => x.UploadedAt).ToList()
            })
            .OrderBy(g => g.MediaType)
            .ToList();

    }
}
