using System;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Remp.Service.Interfaces;

namespace Remp.Service.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly IMemoryCache _cache;

    public BlobStorageService(
        BlobServiceClient blobServiceClient, 
        IConfiguration configuration,
        IMemoryCache cache
        )
    {
        _blobServiceClient = blobServiceClient;
        _containerName = configuration.GetSection("AzureBlobStorage")["ContainerName"];
        _cache = cache;
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(fileName);

        await blobClient.UploadAsync(fileStream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png",
                ".webp" => "image/webp", ".gif" => "image/gif",
                ".pdf" => "application/pdf", ".mp4" => "video/mp4",
                ".webm" => "video/webm", _ => "application/octet-stream"
            } }
        });

        return blobClient.Uri.ToString();
    }

    public async Task DeleteIfExistsAsync(string fileName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(fileName);

        await blobClient.DeleteIfExistsAsync();
    }
}
