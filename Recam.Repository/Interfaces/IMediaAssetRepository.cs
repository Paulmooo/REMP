using System;
using Recam.Models.Entities;

namespace Recam.Repository.Interfaces;

public interface IMediaAssetRepository
{
    Task<MediaAsset?> GetMediaAssetWithListingByIdAsync(int id);
    Task AddMediaAssetsAsync(List<MediaAsset> mediaAssets);
    Task DeleteMediaAssetAsync(MediaAsset mediaAsset);
}
