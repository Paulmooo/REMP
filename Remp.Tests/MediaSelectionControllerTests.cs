using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Remp.API.Controllers;
using Remp.Common.Extensions;
using Remp.Service.DTOs.ListingCase;
using Remp.Service.Interfaces;

namespace Remp.Tests;

public class MediaSelectionControllerTests
{
    [Theory]
    [InlineData("uid")]
    [InlineData(ClaimTypes.NameIdentifier)]
    public async Task Endpoints_PassTrustedIdentityAndReturnState(string claim)
    {
        var service = new Mock<IListingCaseService>(MockBehavior.Strict);
        var result = new FinalSelectionDto { ListingCaseId = 1 };
        var save = new SaveMediaSelectionRequestDto { MediaIds = [2], HeroMediaId = 2 };
        var hero = new SetHeroRequestDto { MediaId = 2 };
        service.Setup(s => s.SaveMediaSelectionAsync(1, save, "agent")).ReturnsAsync(result);
        service.Setup(s => s.SetHeroAsync(1, hero, "agent")).ReturnsAsync(result);
        service.Setup(s => s.GetFinalSelectionAsync(1, "agent")).ReturnsAsync(result);
        var controller = new ListingCaseController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(claim, "agent")], "test")) } }
        };
        foreach (var response in new[] { await controller.SaveMediaSelection(1, save), await controller.SetHero(1, hero), await controller.GetFinalSelection(1) })
            Assert.IsType<ApiResponse<FinalSelectionDto>>(Assert.IsType<OkObjectResult>(response).Value);
        service.VerifyAll();
        foreach (var name in new[] { "SaveMediaSelection", "SetHero" })
            Assert.Equal("AgentPolicy", Assert.Single(typeof(ListingCaseController).GetMethod(name)!
                .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Policy);
        Assert.Equal("Admin,Agent", Assert.Single(typeof(ListingCaseController).GetMethod("GetFinalSelection")!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Roles);
    }
}
