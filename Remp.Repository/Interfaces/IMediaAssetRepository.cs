using System;
using Remp.Models.Entities;

namespace Remp.Repository.Interfaces;

public interface IMediaAssetRepository
{
    Task<MediaAsset?> GetMediaAssetWithListingByIdAsync(int id);
    Task AddMediaAssetsAsync(List<MediaAsset> mediaAssets);
    Task DeleteMediaAssetAsync(MediaAsset mediaAsset);
}
