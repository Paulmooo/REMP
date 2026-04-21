using Microsoft.AspNetCore.Http;
using Recam.Models.Enums;
using Recam.Service.DTOs.ListingCase;

namespace Recam.Service.Interfaces;

public interface IMediaAssetService
{
    Task<List<MediaAssetDto>> UploadMediaAssetsAsync(List<IFormFile> files, MediaType type, int listingCaseId, string userId);
    Task DeleteMediaAssetAsync(int id, string userId);
}
