using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Remp.Models.Entities;
using Remp.Models.Enums;
using Remp.Repository.Interfaces;
using Remp.Service.DTOs.ListingCase;
using Remp.Service.Interfaces;

namespace Remp.Service.Services;

public class MediaAssetService : IMediaAssetService
{
    private readonly IMediaAssetRepository _mediaAssetRepository;
    private readonly IListingCaseRepository _listingCaseRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IMapper _mapper;
    private readonly ILogger<MediaAssetService> _logger;

    public MediaAssetService(
        IMediaAssetRepository mediaAssetRepository,
        IListingCaseRepository listingCaseRepository,
        IBlobStorageService blobStorageService,
        IMapper mapper,
        ILogger<MediaAssetService> logger)
    {
        _mediaAssetRepository = mediaAssetRepository;
        _listingCaseRepository = listingCaseRepository;
        _blobStorageService = blobStorageService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<List<MediaAssetDto>> UploadMediaAssetsAsync(List<IFormFile> files, MediaType type, int listingCaseId, string userId)
    {
        if (!Enum.IsDefined(typeof(MediaType), type))
        {
            throw new ArgumentException("Invalid media type.");
        }

        if (files == null || files.Count < 1)
        {
            throw new ArgumentException("At least one file is required.");
        }

        if (type != MediaType.Picture && files.Count > 1)
        {
            throw new ArgumentException("Only picture type allows multiple file upload.");
        }

        var listingCase = await _listingCaseRepository.GetListingCaseByIdAsync(listingCaseId);
        if (listingCase == null)
        {
            throw new KeyNotFoundException($"Listing case with ID {listingCaseId} not found.");
        }

        if (listingCase.UserId != userId)
        {
            throw new ArgumentException($"User with ID {userId} does not have permission to upload media assets to listing case with ID {listingCaseId}.");
        }

        var mediaAssets = new List<MediaAsset>();
        var attemptedBlobNames = new List<string>();

        try
        {
            foreach (var file in files)
            {
                if (file.Length <= 0)
                {
                    throw new ArgumentException("Uploaded file cannot be empty.");
                }

                await using var stream = file.OpenReadStream();
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                attemptedBlobNames.Add(fileName);
                var mediaUrl = await _blobStorageService.UploadAsync(stream, fileName);

                mediaAssets.Add(new MediaAsset
                {
                    MediaType = type,
                    MediaUrl = mediaUrl,
                    UploadedAt = DateTime.UtcNow,
                    IsSelected = false,
                    IsHero = false,
                    IsDeleted = false,
                    ListingCaseId = listingCaseId,
                    UserId = userId
                });
            }

            await _mediaAssetRepository.AddMediaAssetsAsync(mediaAssets);
        }
        catch
        {
            foreach (var blobName in attemptedBlobNames)
            {
                try
                {
                    await _blobStorageService.DeleteIfExistsAsync(blobName);
                }
                catch (Exception cleanupException)
                {
                    _logger.LogError(
                        cleanupException,
                        "Failed to delete blob {BlobName} while cleaning up a failed media upload.",
                        blobName);
                }
            }

            throw;
        }

        return _mapper.Map<List<MediaAssetDto>>(mediaAssets);
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
