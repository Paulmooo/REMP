using AutoMapper;
using Microsoft.Extensions.Configuration;
using Moq;
using Remp.Common.Exceptions;
using Remp.Repository.Interfaces;
using Remp.Service.DTOs.Auth;
using Remp.Service.Services;

namespace Remp.Tests;

public class AuthServiceTests
{
    [Theory]
    [InlineData("name", "user@example.test", "Valid123!")]
    [InlineData("", "", "")]
    public async Task PublicRegistration_IsDisabledWithoutTouchingIdentity(string name, string email, string password)
    {
        var repository = new Mock<IAuthRepository>();
        var service = new AuthService(new ConfigurationBuilder().Build(), repository.Object, Mock.Of<IMapper>());
        await Assert.ThrowsAsync<ForbiddenException>(() => service.RegisterAsync(new RegisterRequestDto
            { UserName = name, Email = email, Password = password }));
        repository.VerifyNoOtherCalls();
    }
}
