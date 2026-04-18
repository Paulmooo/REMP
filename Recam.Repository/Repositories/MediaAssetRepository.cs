using System;
using Microsoft.EntityFrameworkCore;
using Recam.DataAccess.Data;
using Recam.Models.Entities;
using Recam.Repository.Interfaces;

namespace Recam.Repository.Repositories;

public class MediaAssetRepository : IMediaAssetRepository
{
    private readonly RecamDbContext _dbContext;

    public MediaAssetRepository(RecamDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MediaAsset?> GetMediaAssetWithListingByIdAsync(int id)
    {
        return await _dbContext.MediaAssets
            .Include(x => x.ListingCase)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task DeleteMediaAssetAsync(MediaAsset mediaAsset)
    {
        _dbContext.MediaAssets.Remove(mediaAsset);
        await _dbContext.SaveChangesAsync();
    }
}
