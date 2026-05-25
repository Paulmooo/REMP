using System;
using Azure.Storage.Blobs;
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

        await blobClient.UploadAsync(fileStream);

        return blobClient.Uri.ToString();
    }
}
