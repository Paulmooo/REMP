using AutoMapper;
using FluentAssertions;
using Moq;
using Remp.Models.Entities;
using Remp.Models.Enums;
using Remp.Repository.Interfaces;
using Remp.Service.DTOs.ListingCase;
using Remp.Service.Services;
using Remp.Service.Validators;

namespace Remp.Tests;

public class ListingCaseServiceTests
{
    private readonly Mock<IListingCaseRepository> _repositoryMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly ListingCaseService _service;

    public ListingCaseServiceTests()
    {
        _mapperMock
            .Setup(x => x.Map<ListingCase>(It.IsAny<CreateListingCaseRequestDto>()))
            .Returns<CreateListingCaseRequestDto>(dto => new ListingCase
            {
                Title = dto.Title,
                Description = dto.Description,
                Street = dto.Street,
                City = dto.City,
                State = dto.State,
                PostCode = dto.PostCode,
                Longitude = dto.Longitude,
                Latitude = dto.Latitude,
                Price = dto.Price,
                Bedrooms = dto.Bedrooms,
                Bathrooms = dto.Bathrooms,
                Garages = dto.Garages,
                FloorArea = dto.FloorArea,
                PropertyType = dto.PropertyType,
                SaleCategory = dto.SaleCategory,
                Agents = [],
                CaseContacts = [],
                MediaAssets = [],
                User = null!
            });

        _mapperMock
            .Setup(x => x.Map(It.IsAny<UpdateListingCaseRequestDto>(), It.IsAny<ListingCase>()))
            .Callback<UpdateListingCaseRequestDto, ListingCase>((dto, listingCase) =>
            {
                listingCase.Title = dto.Title;
                listingCase.Description = dto.Description;
                listingCase.Street = dto.Street;
                listingCase.City = dto.City;
                listingCase.State = dto.State;
                listingCase.PostCode = dto.PostCode;
                listingCase.Longitude = dto.Longitude;
                listingCase.Latitude = dto.Latitude;
                listingCase.Price = dto.Price;
                listingCase.Bedrooms = dto.Bedrooms;
                listingCase.Bathrooms = dto.Bathrooms;
                listingCase.Garages = dto.Garages;
                listingCase.FloorArea = dto.FloorArea;
                listingCase.PropertyType = dto.PropertyType;
                listingCase.SaleCategory = dto.SaleCategory;
            });

        _service = new ListingCaseService(
            _repositoryMock.Object,
            _mapperMock.Object,
            new CreateListingCaseRequestValidator(),
            new UpdateListingCaseRequestValidator());
    }

    [Fact]
    public async Task CreateListingCaseAsync_WithValidInput_ShouldPersistListingCase()
    {
        var dto = BuildCreateDto();
        ListingCase? capturedCase = null;

        _repositoryMock.Setup(x => x.UserExistsAsync("owner-1")).ReturnsAsync(true);
        _repositoryMock
            .Setup(x => x.CreateListingCaseAsync(It.IsAny<ListingCase>()))
            .Callback<ListingCase>(listingCase => capturedCase = listingCase)
            .ReturnsAsync(42);

        var before = DateTime.UtcNow;
        var result = await _service.CreateListingCaseAsync(dto, "owner-1");
        var after = DateTime.UtcNow;

        result.Id.Should().Be(42);
        result.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        capturedCase.Should().NotBeNull();
        capturedCase!.Title.Should().Be(dto.Title);
        capturedCase.UserId.Should().Be("owner-1");
        capturedCase.IsDeleted.Should().BeFalse();
        capturedCase.ListingStatus.Should().Be(ListcaseStatus.Created);
        capturedCase.CreatedAt.Should().Be(result.CreatedAt);

        _repositoryMock.Verify(x => x.CreateListingCaseAsync(It.IsAny<ListingCase>()), Times.Once);
    }

    [Fact]
    public async Task CreateListingCaseAsync_WithInvalidInput_ShouldThrowArgumentException()
    {
        var dto = BuildCreateDto();
        dto.Title = " ";
        dto.PostCode = 0;

        var act = () => _service.CreateListingCaseAsync(dto, "owner-1");

        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Contain("Title is required.");
        exception.Which.Message.Should().Contain("PostCode must be a positive integer.");

        _repositoryMock.Verify(x => x.UserExistsAsync(It.IsAny<string>()), Times.Never);
        _repositoryMock.Verify(x => x.CreateListingCaseAsync(It.IsAny<ListingCase>()), Times.Never);
    }

    [Fact]
    public async Task CreateListingCaseAsync_WhenUserDoesNotExist_ShouldThrowUnauthorizedAccessException()
    {
        _repositoryMock.Setup(x => x.UserExistsAsync("missing-user")).ReturnsAsync(false);

        var act = () => _service.CreateListingCaseAsync(BuildCreateDto(), "missing-user");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("The user in the JWT token does not exist.");
    }

    [Fact]
    public async Task UpdateListingCaseAsync_WithValidInput_ShouldUpdateMutableFieldsOnly()
    {
        var existingCase = BuildListingCase();
        var dto = BuildUpdateDto();
        dto.Title = "Updated title";
        dto.Price = 999_000;

        _repositoryMock.Setup(x => x.UserExistsAsync("owner-1")).ReturnsAsync(true);
        _repositoryMock.Setup(x => x.GetListingCaseByIdAsync(7)).ReturnsAsync(existingCase);

        await _service.UpdateListingCaseAsync(7, dto, "owner-1");

        existingCase.Title.Should().Be("Updated title");
        existingCase.Price.Should().Be(999_000);
        existingCase.ListingStatus.Should().Be(ListcaseStatus.Pending);

        _repositoryMock.Verify(x => x.UpdateListingCaseAsync(existingCase), Times.Once);
    }

    [Fact]
    public async Task UpdateListingCaseAsync_WithInvalidInput_ShouldThrowArgumentException()
    {
        _repositoryMock.Setup(x => x.UserExistsAsync("owner-1")).ReturnsAsync(true);

        var dto = BuildUpdateDto();
        dto.Title = "";

        var act = () => _service.UpdateListingCaseAsync(7, dto, "owner-1");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Title is required.*");

        _repositoryMock.Verify(x => x.GetListingCaseByIdAsync(It.IsAny<int>()), Times.Never);
        _repositoryMock.Verify(x => x.UpdateListingCaseAsync(It.IsAny<ListingCase>()), Times.Never);
    }

    [Fact]
    public async Task UpdateListingCaseAsync_WhenListingCaseDoesNotExist_ShouldThrowKeyNotFoundException()
    {
        _repositoryMock.Setup(x => x.UserExistsAsync("owner-1")).ReturnsAsync(true);
        _repositoryMock.Setup(x => x.GetListingCaseByIdAsync(7)).ReturnsAsync((ListingCase?)null);

        var act = () => _service.UpdateListingCaseAsync(7, BuildUpdateDto(), "owner-1");

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Listing case with ID 7 not found.");
    }

    [Fact]
    public async Task DeleteListingCaseAsync_WhenUserDoesNotOwnListingCase_ShouldThrowUnauthorizedAccessException()
    {
        _repositoryMock.Setup(x => x.UserExistsAsync("other-user")).ReturnsAsync(true);
        _repositoryMock.Setup(x => x.GetListingCaseByIdAsync(7)).ReturnsAsync(BuildListingCase());

        var act = () => _service.DeleteListingCaseAsync(7, "other-user");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Users can only delete their own listing cases.");
    }

    [Fact]
    public async Task DeleteListingCaseAsync_WhenListingCaseDoesNotExist_ShouldThrowKeyNotFoundException()
    {
        _repositoryMock.Setup(x => x.UserExistsAsync("owner-1")).ReturnsAsync(true);
        _repositoryMock.Setup(x => x.GetListingCaseByIdAsync(7)).ReturnsAsync((ListingCase?)null);

        var act = () => _service.DeleteListingCaseAsync(7, "owner-1");

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Listing case with ID 7 not found.");
    }

    [Fact]
    public async Task ChangeListingCaseStatusAsync_WhenOwnerMakesValidTransition_ShouldUpdateStatus()
    {
        var existingCase = BuildListingCase(status: ListcaseStatus.Created);

        _repositoryMock.Setup(x => x.UserExistsAsync("owner-1")).ReturnsAsync(true);
        _repositoryMock.Setup(x => x.GetListingCaseByIdAsync(7)).ReturnsAsync(existingCase);

        await _service.ChangeListingCaseStatusAsync(7, ListcaseStatus.Pending, "owner-1");

        existingCase.ListingStatus.Should().Be(ListcaseStatus.Pending);
        _repositoryMock.Verify(x => x.UpdateListingCaseAsync(existingCase), Times.Once);
    }

    [Fact]
    public async Task ChangeListingCaseStatusAsync_WhenAssignedAgentMakesValidTransition_ShouldUpdateStatus()
    {
        var existingCase = BuildListingCase(status: ListcaseStatus.Pending, assignedAgentIds: ["agent-1"]);

        _repositoryMock.Setup(x => x.UserExistsAsync("agent-1")).ReturnsAsync(true);
        _repositoryMock.Setup(x => x.GetListingCaseByIdAsync(7)).ReturnsAsync(existingCase);

        await _service.ChangeListingCaseStatusAsync(7, ListcaseStatus.Delivered, "agent-1");

        existingCase.ListingStatus.Should().Be(ListcaseStatus.Delivered);
        _repositoryMock.Verify(x => x.UpdateListingCaseAsync(existingCase), Times.Once);
    }

    [Fact]
    public async Task ChangeListingCaseStatusAsync_WhenListingCaseIsDeleted_ShouldThrowArgumentException()
    {
        var existingCase = BuildListingCase();
        existingCase.IsDeleted = true;

        _repositoryMock.Setup(x => x.UserExistsAsync("owner-1")).ReturnsAsync(true);
        _repositoryMock.Setup(x => x.GetListingCaseByIdAsync(7)).ReturnsAsync(existingCase);

        var act = () => _service.ChangeListingCaseStatusAsync(7, ListcaseStatus.Pending, "owner-1");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Deleted listing cases cannot be updated.");
    }

    private static CreateListingCaseRequestDto BuildCreateDto() =>
        new()
        {
            Title = "12 Sample Street",
            Description = "A solid fixture for tests.",
            Street = "12 Sample Street",
            City = "Sydney",
            State = "NSW",
            PostCode = 2000,
            Longitude = 151.2093m,
            Latitude = -33.8688m,
            Price = 850_000,
            Bedrooms = 3,
            Bathrooms = 2,
            Garages = 1,
            FloorArea = 120.5,
            PropertyType = PropertyType.House,
            SaleCategory = SaleCategory.ForSale
        };

    private static UpdateListingCaseRequestDto BuildUpdateDto() =>
        new()
        {
            Title = "Updated listing",
            Description = "Updated description",
            Street = "99 Update Avenue",
            City = "Melbourne",
            State = "VIC",
            PostCode = 3000,
            Longitude = 144.9631m,
            Latitude = -37.8136m,
            Price = 910_000,
            Bedrooms = 4,
            Bathrooms = 3,
            Garages = 2,
            FloorArea = 150.0,
            PropertyType = PropertyType.Unit,
            SaleCategory = SaleCategory.Auction
        };

    private static ListingCase BuildListingCase(
        string ownerId = "owner-1",
        ListcaseStatus status = ListcaseStatus.Pending,
        params string[] assignedAgentIds) =>
        new()
        {
            Id = 7,
            Title = "Existing listing",
            Description = "Existing description",
            Street = "1 Existing Street",
            City = "Sydney",
            State = "NSW",
            PostCode = 2000,
            Longitude = 151.2m,
            Latitude = -33.8m,
            Price = 750_000,
            Bedrooms = 2,
            Bathrooms = 1,
            Garages = 1,
            FloorArea = 88.5,
            CreatedAt = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
            IsDeleted = false,
            PropertyType = PropertyType.House,
            SaleCategory = SaleCategory.ForSale,
            ListingStatus = status,
            UserId = ownerId,
            User = null!,
            Agents = assignedAgentIds.Select(id => new Agent
            {
                Id = id,
                AgentFirstName = "Test",
                AgentLastName = "Agent",
                AvatarUrl = string.Empty,
                CompanyName = "Remp",
                User = null!,
                ListingCases = [],
                PhotographyCompanies = []
            }).ToList(),
            CaseContacts = [],
            MediaAssets = []
        };
}
