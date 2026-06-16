using Remp.Models.Enums;
using Remp.Service.DTOs.ListingCase;

namespace Remp.Service.Interfaces;

public interface IListingCaseService
{
    Task<CreateListingCaseResponseDto> CreateListingCaseAsync(CreateListingCaseRequestDto dto, string userId);
    Task UpdateListingCaseAsync(int id, UpdateListingCaseRequestDto dto, string userId);
    Task<PagedListingCasesResponseDto> GetAllListingCasesAsync(int pageNumber, int pageSize, string userId, string role);
    Task DeleteListingCaseAsync(int id, string userId);
    Task<ListingCaseItemDto> GetListingCaseDetailsAsync(int id, string userId, string role);
    Task ChangeListingCaseStatusAsync(int id, ListcaseStatus newStatus, string userId);
    Task<List<MediaAssetGroupDto>> GetListingCaseMediaAssetsAsync(int listingCaseId, string userId, string role);

}
