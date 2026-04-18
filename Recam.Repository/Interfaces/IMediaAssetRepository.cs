using System;
using Recam.Models.Entities;

namespace Recam.Repository.Interfaces;

public interface IMediaAssetRepository
{
    Task<MediaAsset?> GetMediaAssetWithListingByIdAsync(int id);
    Task DeleteMediaAssetAsync(MediaAsset mediaAsset);
    
}
