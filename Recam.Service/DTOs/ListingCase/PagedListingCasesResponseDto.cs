using System;

namespace Recam.Service.DTOs.ListingCase;

public class PagedListingCasesResponseDto
{
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public List<ListingCaseItemDto> ListingCases { get; set; }
}
