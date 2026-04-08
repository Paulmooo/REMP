using System;
using Recam.Service.DTOs;

namespace Recam.Service.Interfaces;

public interface IListingCaseService
{
    Task<CreateListingCaseResponseDto> CreateListingCaseAsync(CreateListingCaseRequestDto dto, string userId);
    Task UpdateListingCaseAsync(int id, UpdateListingCaseRequestDto dto, string userId);

}
