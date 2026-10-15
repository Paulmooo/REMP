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
}
