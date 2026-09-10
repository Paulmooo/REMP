using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Remp.Models.Entities;
using Remp.Models.Enums;
using Remp.Repository.Interfaces;
using Remp.Service.DTOs.ListingCase;
using Remp.Service.Interfaces;
using Remp.Service.Services;

namespace Remp.Tests;

public class MediaAssetServiceTests
{
    private readonly Mock<IMediaAssetRepository> _mediaAssetRepositoryMock = new();
    private readonly Mock<IListingCaseRepository> _listingCaseRepositoryMock = new();
    private readonly Mock<IBlobStorageService> _blobStorageServiceMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly Mock<ILogger<MediaAssetService>> _loggerMock = new();
    private readonly MediaAssetService _service;

    public MediaAssetServiceTests()
    {
        _listingCaseRepositoryMock
            .Setup(x => x.GetListingCaseByIdAsync(7))
            .ReturnsAsync(new ListingCase { Id = 7, UserId = "owner-1" });

        _service = new MediaAssetService(
            _mediaAssetRepositoryMock.Object,
            _listingCaseRepositoryMock.Object,
            _blobStorageServiceMock.Object,
            _mapperMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task UploadMediaAssetsAsync_WhenSecondUploadFails_ShouldCleanUpAttemptedBlobsAndRethrowOriginalException()
    {
        var uploadException = new InvalidOperationException("Second upload failed.");
        var attemptedBlobNames = new List<string>();
        var deletedBlobNames = new List<string>();
        var uploadCount = 0;

        _blobStorageServiceMock
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .Returns((Stream _, string blobName) =>
            {
                attemptedBlobNames.Add(blobName);
                uploadCount++;

                return uploadCount == 2
                    ? Task.FromException<string>(uploadException)
                    : Task.FromResult($"https://storage.test/{blobName}");
            });
        _blobStorageServiceMock
            .Setup(x => x.DeleteIfExistsAsync(It.IsAny<string>()))
            .Callback<string>(blobName => deletedBlobNames.Add(blobName))
            .Returns(Task.CompletedTask);

        var act = () => _service.UploadMediaAssetsAsync(
            [BuildFile("first.jpg"), BuildFile("second.jpg")],
            MediaType.Picture,
            7,
            "owner-1");

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();

        thrown.Which.Should().BeSameAs(uploadException);
        attemptedBlobNames.Should().HaveCount(2);
        deletedBlobNames.Should().BeEquivalentTo(attemptedBlobNames);

        _mediaAssetRepositoryMock.Verify(
            x => x.AddMediaAssetsAsync(It.IsAny<List<MediaAsset>>()),
            Times.Never);
    }

    [Fact]
    public async Task UploadMediaAssetsAsync_WhenDatabaseSaveFails_ShouldCleanUpAllBlobsAndRethrowOriginalException()
    {
        var persistenceException = new InvalidOperationException("Database save failed.");
        var attemptedBlobNames = SetupSuccessfulUploads();
        var deletedBlobNames = new List<string>();
        _mediaAssetRepositoryMock
            .Setup(x => x.AddMediaAssetsAsync(It.IsAny<List<MediaAsset>>()))
            .ThrowsAsync(persistenceException);
        _blobStorageServiceMock
            .Setup(x => x.DeleteIfExistsAsync(It.IsAny<string>()))
            .Callback<string>(blobName => deletedBlobNames.Add(blobName))
            .Returns(Task.CompletedTask);

        var act = () => _service.UploadMediaAssetsAsync(
            [BuildFile("first.jpg"), BuildFile("second.jpg")],
            MediaType.Picture,
            7,
            "owner-1");

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();

        thrown.Which.Should().BeSameAs(persistenceException);
        attemptedBlobNames.Should().HaveCount(2);
        deletedBlobNames.Should().BeEquivalentTo(attemptedBlobNames);
    }

    [Fact]
    public async Task UploadMediaAssetsAsync_WhenCleanupFails_ShouldLogAndRethrowOriginalException()
    {
        var persistenceException = new InvalidOperationException("Database save failed.");
        var cleanupException = new InvalidOperationException("Blob cleanup failed.");
        var attemptedBlobNames = SetupSuccessfulUploads();
        var cleanupCount = 0;

        _mediaAssetRepositoryMock
            .Setup(x => x.AddMediaAssetsAsync(It.IsAny<List<MediaAsset>>()))
            .ThrowsAsync(persistenceException);
        _blobStorageServiceMock
            .Setup(x => x.DeleteIfExistsAsync(It.IsAny<string>()))
            .Returns((string _) =>
            {
                cleanupCount++;
                return cleanupCount == 1
                    ? Task.FromException(cleanupException)
                    : Task.CompletedTask;
            });

        var act = () => _service.UploadMediaAssetsAsync(
            [BuildFile("first.jpg"), BuildFile("second.jpg")],
            MediaType.Picture,
            7,
            "owner-1");

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();

        thrown.Which.Should().BeSameAs(persistenceException);
        cleanupCount.Should().Be(2);
        attemptedBlobNames.Should().HaveCount(2);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(attemptedBlobNames[0])),
                cleanupException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadMediaAssetsAsync_WhenUploadsAndSaveSucceed_ShouldReturnMappedAssetsWithoutCleanup()
    {
        SetupSuccessfulUploads();
        List<MediaAsset>? persistedAssets = null;
        var expectedDtos = new List<MediaAssetDto>
        {
            new() { Id = 1, MediaType = MediaType.Picture, MediaUrl = "https://storage.test/first" },
            new() { Id = 2, MediaType = MediaType.Picture, MediaUrl = "https://storage.test/second" }
        };

        _mediaAssetRepositoryMock
            .Setup(x => x.AddMediaAssetsAsync(It.IsAny<List<MediaAsset>>()))
            .Callback<List<MediaAsset>>(assets => persistedAssets = assets)
            .Returns(Task.CompletedTask);
        _mapperMock
            .Setup(x => x.Map<List<MediaAssetDto>>(It.IsAny<List<MediaAsset>>()))
            .Returns(expectedDtos);

        var result = await _service.UploadMediaAssetsAsync(
            [BuildFile("first.jpg"), BuildFile("second.jpg")],
            MediaType.Picture,
            7,
            "owner-1");

        result.Should().BeSameAs(expectedDtos);
        persistedAssets.Should().HaveCount(2);
        _blobStorageServiceMock.Verify(
            x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()),
            Times.Exactly(2));
        _blobStorageServiceMock.Verify(
            x => x.DeleteIfExistsAsync(It.IsAny<string>()),
            Times.Never);
    }

    private List<string> SetupSuccessfulUploads()
    {
        var attemptedBlobNames = new List<string>();
        _blobStorageServiceMock
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .Returns((Stream _, string blobName) =>
            {
                attemptedBlobNames.Add(blobName);
                return Task.FromResult($"https://storage.test/{blobName}");
            });

        return attemptedBlobNames;
    }

    private static IFormFile BuildFile(string fileName)
    {
        var file = new Mock<IFormFile>();
        file.SetupGet(x => x.FileName).Returns(fileName);
        file.SetupGet(x => x.Length).Returns(1);
        file.Setup(x => x.OpenReadStream()).Returns(() => new MemoryStream([1]));
        return file.Object;
    }
}
