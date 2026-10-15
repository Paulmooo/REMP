using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Remp.API.Controllers;
using Remp.Service.DTOs.User;
using Remp.Service.Interfaces;

namespace Remp.Tests;

public class UserControllerTests
{
    [Theory]
    [InlineData("uid")]
    [InlineData(ClaimTypes.NameIdentifier)]
    public async Task Link_PassesAuthenticatedCallerSeparatelyFromTargetIds(string claimType)
    {
        var service = new Mock<IUserService>();
        var controller = new UserController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(claimType, "caller")], "test"))
                }
            }
        };
        await controller.AddAgentToPhotographyCompany("target-company", new AddAgentToPhotographyCompanyRequestDto { AgentId = "target-agent" });
        service.Verify(x => x.AddAgentToPhotographyCompanyAsync("caller", "target-agent", "target-company"), Times.Once);
    }

    [Fact]
    public async Task Link_WithoutIdentityClaim_DoesNotCallService()
    {
        var service = new Mock<IUserService>();
        var controller = new UserController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.AddAgentToPhotographyCompany("company", new AddAgentToPhotographyCompanyRequestDto { AgentId = "agent" }));
        service.VerifyNoOtherCalls();
    }
    [Theory]
    [InlineData("uid")]
    [InlineData(ClaimTypes.NameIdentifier)]
    public async Task AccountEndpoints_UseCallerIdentity(string claimType)
    {
        var service = new Mock<IUserService>();
        var controller = new UserController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(claimType, "caller")], "test"))
                }
            }
        };
        service.Setup(x => x.GetAgentByEmailAsync("caller", "agent@example.test")).ReturnsAsync(new GetAgentResponseDto());
        await controller.GetAgentByEmail("agent@example.test");
        await controller.GetCompanyAgentList();
        var password = new UpdatePasswordRequestDto { CurrentPassword = "Current123!", NewPassword = "New123!" };
        await controller.UpdatePassword(password);
        service.Verify(x => x.GetAgentByEmailAsync("caller", "agent@example.test"), Times.Once);
        service.Verify(x => x.GetAgentsByCompanyIdAsync("caller"), Times.Once);
        service.Verify(x => x.UpdatePasswordAsync("caller", password), Times.Once);
    }

    [Fact]
    public async Task AccountEndpoints_MissingClaimsRejectBeforeService()
    {
        var service = new Mock<IUserService>();
        var controller = new UserController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.GetAgentByEmail("agent@example.test"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.GetCompanyAgentList());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.UpdatePassword(new UpdatePasswordRequestDto()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.CreateAgent(new CreateAgentRequestDto()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.GetCurrentUserInfo());
        service.VerifyNoOtherCalls();
    }
}
