using AutoMapper;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Moq;
using Remp.Common.Exceptions;
using Remp.Models.Entities;
using Remp.Repository.Interfaces;
using Remp.Service.DTOs.User;
using Remp.Service.Services;
using Remp.Service.Validators;

namespace Remp.Tests;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _repository = new();
    private readonly UserService _service;
    private readonly PhotographyCompany _company = new() { Id = "company-a", Agents = [] };
    private readonly Agent _agent = new() { Id = "agent" };

    public UserServiceTests()
    {
        _service = new UserService(_repository.Object, new CreateAgentRequestValidator(),
            new UpdatePasswordRequestValidator(), Mock.Of<IMapper>());
        _repository.Setup(x => x.GetPhotographyCompanyByIdAsync("company-a")).ReturnsAsync(_company);
        _repository.Setup(x => x.GetAgentByIdAsync("agent")).ReturnsAsync(_agent);
    }

    [Fact]
    public async Task Link_OwnCompany_PersistsRelationship()
    {
        await _service.AddAgentToPhotographyCompanyAsync("company-a", "agent", "company-a");
        _repository.Verify(x => x.AddAgentToPhotographyCompany(_company, _agent), Times.Once);
    }

    [Fact]
    public async Task Link_OtherCompany_RejectsBeforeReadingOrWriting()
    {
        var act = () => _service.AddAgentToPhotographyCompanyAsync("company-b", "agent", "company-a");
        await act.Should().ThrowAsync<ForbiddenException>();
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Link_Duplicate_ReturnsConflictWithoutWriting()
    {
        _company.Agents.Add(_agent);
        var act = () => _service.AddAgentToPhotographyCompanyAsync("company-a", "agent", "company-a");
        await act.Should().ThrowAsync<ConflictException>();
        _repository.Verify(x => x.AddAgentToPhotographyCompany(It.IsAny<PhotographyCompany>(), It.IsAny<Agent>()), Times.Never);
        _company.Agents.Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Link_EmptyAgentId_IsInvalid(string? agentId)
    {
        var act = () => _service.AddAgentToPhotographyCompanyAsync("company-a", agentId!, "company-a");
        await act.Should().ThrowAsync<ArgumentException>();
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Link_MissingAgent_IsNotFound()
    {
        var act = () => _service.AddAgentToPhotographyCompanyAsync("company-a", "missing", "company-a");
        await act.Should().ThrowAsync<KeyNotFoundException>();
        _repository.Verify(x => x.AddAgentToPhotographyCompany(It.IsAny<PhotographyCompany>(), It.IsAny<Agent>()), Times.Never);
    }

    [Fact]
    public async Task Create_InvalidDto_DoesNotCreateAccount()
    {
        var act = () => _service.CreateAgentAsync("company-a", new CreateAgentRequestDto());
        await act.Should().ThrowAsync<ValidationException>();
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_WithoutCompany_IsForbidden()
    {
        var act = () => _service.CreateAgentAsync("other", ValidDto());
        await act.Should().ThrowAsync<ForbiddenException>();
        _repository.Verify(x => x.CreateAgentAsync(It.IsAny<Agent>(), It.IsAny<User>(), It.IsAny<string>(), It.IsAny<PhotographyCompany>()), Times.Never);
    }

    [Fact]
    public async Task Create_DuplicateIdentity_ReturnsConflict()
    {
        _repository.Setup(x => x.CreateAgentAsync(It.IsAny<Agent>(), It.IsAny<User>(), It.IsAny<string>(), _company))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "DuplicateUserName", Description = "Duplicate" }));
        var act = () => _service.CreateAgentAsync("company-a", ValidDto());
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Create_IdentityValidationFailure_IsBadInput()
    {
        _repository.Setup(x => x.CreateAgentAsync(It.IsAny<Agent>(), It.IsAny<User>(), It.IsAny<string>(), _company))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "InvalidUserName", Description = "Invalid" }));
        var act = () => _service.CreateAgentAsync("company-a", ValidDto());
        await act.Should().ThrowAsync<ArgumentException>();
    }

    internal static CreateAgentRequestDto ValidDto() => new()
    {
        Email = "agent@example.test", AgentFirstName = "Test", AgentLastName = "Agent", AvatarUrl = ""
    };
}
