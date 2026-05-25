using Microsoft.AspNetCore.Http;
using Remp.Models.Enums;
using Remp.Service.DTOs.ListingCase;

namespace Remp.Service.Interfaces;

public interface IMediaAssetService
{
    Task<List<MediaAssetDto>> UploadMediaAssetsAsync(List<IFormFile> files, MediaType type, int listingCaseId, string userId);
    Task DeleteMediaAssetAsync(int id, string userId);
}
