using Recam.Service.DTOs.ListingCase;

namespace Recam.Service.Interfaces;

public interface IListingCaseService
{
    Task<CreateListingCaseResponseDto> CreateListingCaseAsync(CreateListingCaseRequestDto dto, string userId);
    Task UpdateListingCaseAsync(int id, UpdateListingCaseRequestDto dto, string userId);
    Task<PagedListingCasesResponseDto> GetAllListingCasesAsync(int pageNumber, int pageSize, string userId, string role);
    Task DeleteListingCaseAsync(int id, string userId);
}
