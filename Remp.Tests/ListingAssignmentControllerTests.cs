using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Remp.API.Controllers;
using Remp.Service.DTOs.ListingCase;
using Remp.Service.Interfaces;

namespace Remp.Tests;

public class ListingAssignmentControllerTests
{
    [Theory]
    [InlineData("uid", true)]
    [InlineData(ClaimTypes.NameIdentifier, true)]
    [InlineData("uid", false)]
    [InlineData(ClaimTypes.NameIdentifier, false)]
    public async Task Assignment_PassesTrustedIdentityAndReturnsDto(string claim, bool assign)
    {
        var service = new Mock<IListingCaseService>(MockBehavior.Strict);
        var dto = new ListingAgentAssignmentDto { ListingCaseId = 1, AgentId = "agent", Assigned = assign };
        if (assign) service.Setup(s => s.AssignAgentAsync(1, "agent", "company")).ReturnsAsync(dto);
        else service.Setup(s => s.UnassignAgentAsync(1, "agent", "company")).ReturnsAsync(dto);
        var controller = new ListingCaseController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(claim, "company")], "test")) } }
        };
        Assert.IsType<OkObjectResult>(assign ? await controller.AssignAgent(1, "agent") : await controller.UnassignAgent(1, "agent"));
        service.VerifyAll();
        var method = typeof(ListingCaseController).GetMethod(assign ? "AssignAgent" : "UnassignAgent")!;
        Assert.Equal("AdminPolicy", Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Policy);
    }
}
