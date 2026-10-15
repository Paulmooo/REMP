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
        _listingCaseRepositoryMock.Setup(x => x.IsActiveCompanyAdminAsync("owner-1")).ReturnsAsync(true);
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

    [Theory]
    [InlineData(MediaType.Video)]
    [InlineData(MediaType.FloorPlan)]
    [InlineData(MediaType.VRTour)]
    public async Task NonPicturesRejectMultipleFiles(MediaType type)
    {
        var act = () => _service.UploadMediaAssetsAsync([BuildFile("a.jpg"), BuildFile("b.jpg")], type, 7, "owner-1");
        await act.Should().ThrowAsync<ArgumentException>();
        VerifyNoUploads();
    }

    [Theory]
    [InlineData("owner-1", false, false, false, typeof(Remp.Common.Exceptions.ForbiddenException))]
    [InlineData("other", true, false, false, typeof(Remp.Common.Exceptions.ForbiddenException))]
    [InlineData("owner-1", true, true, false, typeof(KeyNotFoundException))]
    [InlineData("owner-1", true, false, true, typeof(KeyNotFoundException))]
    public async Task UploadChecksIdentityAndListing(string user, bool active, bool deleted, bool missing, Type exception)
    {
        _listingCaseRepositoryMock.Setup(x => x.IsActiveCompanyAdminAsync(user)).ReturnsAsync(active);
        _listingCaseRepositoryMock.Setup(x => x.GetListingCaseByIdAsync(7)).ReturnsAsync(missing ? null : new ListingCase { Id = 7, UserId = "owner-1", IsDeleted = deleted });
        var thrown = await Record.ExceptionAsync(() => _service.UploadMediaAssetsAsync([BuildFile("a.jpg")], MediaType.Picture, 7, user));
        thrown.Should().BeOfType(exception);
        VerifyNoUploads();
    }

    [Theory]
    [InlineData("a.exe", 3, MediaType.Picture)]
    [InlineData("a.mp4", 3, MediaType.Picture)]
    [InlineData("a.jpg", 20971521, MediaType.Picture)]
    [InlineData("a.jpg", 0, MediaType.Picture)]
    [InlineData("a.mp4", 104857601, MediaType.Video)]
    [InlineData("a.zip", 3, MediaType.VRTour)]
    public async Task InvalidFilesNeverReachBlob(string name, long size, MediaType type)
    {
        var file = new Mock<IFormFile>();
        file.SetupGet(x => x.FileName).Returns(name);
        file.SetupGet(x => x.Length).Returns(size);
        var act = () => _service.UploadMediaAssetsAsync([file.Object], type, 7, "owner-1");
        await act.Should().ThrowAsync<ArgumentException>();
        VerifyNoUploads();
    }

    [Fact]
    public async Task HeaderMismatchInSecondFileRejectsEntireBatchBeforeStorage()
    {
        var file = new Mock<IFormFile>();
        file.SetupGet(x => x.FileName).Returns("fake.png");
        file.SetupGet(x => x.Length).Returns(3);
        file.Setup(x => x.OpenReadStream()).Returns(() => new MemoryStream([1, 2, 3]));
        var act = () => _service.UploadMediaAssetsAsync([BuildFile("ok.jpg"), file.Object], MediaType.Picture, 7, "owner-1");
        await act.Should().ThrowAsync<ArgumentException>();
        VerifyNoUploads();
    }

    [Fact]
    public async Task DeliveredUploadStillAllowed()
    {
        _listingCaseRepositoryMock.Setup(x => x.GetListingCaseByIdAsync(7)).ReturnsAsync(new ListingCase { Id = 7, UserId = "owner-1", ListingStatus = ListcaseStatus.Delivered });
        SetupSuccessfulUploads();
        await _service.UploadMediaAssetsAsync([BuildFile("a.jpg")], MediaType.Picture, 7, "owner-1");
        _mediaAssetRepositoryMock.Verify(x => x.AddMediaAssetsAsync(It.IsAny<List<MediaAsset>>()), Times.Once);
    }

    [Theory]
    [InlineData("a.png", MediaType.Picture, "89504E470D0A1A0A")]
    [InlineData("a.gif", MediaType.Picture, "474946383961")]
    [InlineData("a.webp", MediaType.FloorPlan, "524946463030303057454250")]
    [InlineData("a.pdf", MediaType.FloorPlan, "255044462D312E37")]
    [InlineData("a.mp4", MediaType.Video, "303030306674797069736F6D")]
    [InlineData("a.webm", MediaType.Video, "1A45DFA3")]
    public async Task AgreedFormatsPassServerValidation(string name, MediaType type, string hex)
    {
        var bytes = Convert.FromHexString(hex);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "files", name);
        SetupSuccessfulUploads();
        await _service.UploadMediaAssetsAsync([file], type, 7, "owner-1");
        _mediaAssetRepositoryMock.Verify(x => x.AddMediaAssetsAsync(It.IsAny<List<MediaAsset>>()), Times.Once);
    }

    [Fact]
    public async Task PictureCountAndBatchLimitAreEnforcedBeforeStorage()
    {
        var tooMany = Enumerable.Range(0, 21).Select(i => BuildFile($"{i}.jpg")).ToList();
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UploadMediaAssetsAsync(tooMany, MediaType.Picture, 7, "owner-1"));
        var large = new Mock<IFormFile>();
        large.SetupGet(x => x.Length).Returns(20 * 1024 * 1024);
        var tooLarge = Enumerable.Repeat(large.Object, 6).ToList();
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UploadMediaAssetsAsync(tooLarge, MediaType.Picture, 7, "owner-1"));
        VerifyNoUploads();
    }

    private void VerifyNoUploads() => _blobStorageServiceMock.Verify(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()), Times.Never);

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
        file.SetupGet(x => x.Length).Returns(3);
        file.Setup(x => x.OpenReadStream()).Returns(() => new MemoryStream([255,216,255]));
        return file.Object;
    }
}
