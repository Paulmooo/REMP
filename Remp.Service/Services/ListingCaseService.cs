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
