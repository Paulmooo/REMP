using System;
using Microsoft.EntityFrameworkCore;
using Remp.DataAccess.Data;
using Remp.Models.Entities;
using Remp.Repository.Interfaces;

namespace Remp.Repository.Repositories;

public class MediaAssetRepository : IMediaAssetRepository
{
    private readonly RempDbContext _dbContext;

    public MediaAssetRepository(RempDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MediaAsset?> GetMediaAssetWithListingByIdAsync(int id)
    {
        return await _dbContext.MediaAssets
            .Include(x => x.ListingCase)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task AddMediaAssetsAsync(List<MediaAsset> mediaAssets)
    {
        await _dbContext.MediaAssets.AddRangeAsync(mediaAssets);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteMediaAssetAsync(MediaAsset mediaAsset)
    {
        _dbContext.MediaAssets.Remove(mediaAsset);
        await _dbContext.SaveChangesAsync();
    }
}
