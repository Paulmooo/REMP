using Recam.Repository.Interfaces;
using Recam.Service.Interfaces;

namespace Recam.Service.Services;

public class MediaAssetService : IMediaAssetService
{
    private readonly IMediaAssetRepository _mediaAssetRepository;

    public MediaAssetService(IMediaAssetRepository mediaAssetRepository)
    {
        _mediaAssetRepository = mediaAssetRepository;
    }

    public async Task DeleteMediaAssetAsync(int id, string userId)
    {
        var media = await _mediaAssetRepository.GetMediaAssetWithListingByIdAsync(id);
        if (media == null)
        {
            throw new KeyNotFoundException($"Media asset with ID {id} not found.");
        }

        if (media.ListingCase == null || media.ListingCase.UserId != userId)
        {
            throw new ArgumentException($"User with ID {userId} does not have permission to delete media asset with ID {id}.");
        }

        await _mediaAssetRepository.DeleteMediaAssetAsync(media);
    }
}
