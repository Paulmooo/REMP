using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Remp.API.Controllers;
using Remp.Common.Extensions;
using Remp.Service.DTOs.ListingCase;
using Remp.Service.Interfaces;

namespace Remp.Tests;

public class ListingContactControllerTests
{
    [Theory]
    [InlineData("uid")]
    [InlineData(ClaimTypes.NameIdentifier)]
    public async Task Endpoints_UseTrustedIdentityAndReturnContactDtos(string claim)
    {
        var service = new Mock<IListingCaseService>(MockBehavior.Strict);
        var request = new CreateCaseContactRequestDto { FirstName = "Alex", LastName = "Chen" };
        var contact = new CaseContactDto { ContactId = 42 };
        service.Setup(s => s.GetContactsAsync(1, "agent")).ReturnsAsync([contact]);
        service.Setup(s => s.AddContactAsync(1, request, "agent")).ReturnsAsync(contact);
        var controller = new ListingCaseController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(claim, "agent")], "test")) } }
        };
        var read = Assert.IsType<ApiResponse<List<CaseContactDto>>>(Assert.IsType<OkObjectResult>(await controller.GetContacts(1)).Value);
        var created = Assert.IsType<ApiResponse<CaseContactDto>>(Assert.IsType<OkObjectResult>(await controller.AddContact(1, request)).Value);
        service.VerifyAll();
        Assert.Equal("AgentPolicy", Assert.Single(typeof(ListingCaseController).GetMethod("AddContact")!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Policy);
        Assert.Equal("Admin,Agent", Assert.Single(typeof(ListingCaseController).GetMethod("GetContacts")!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Roles);
    }

    [Theory]
    [InlineData("contactId")]
    [InlineData("listingCaseId")]
    public void Request_RejectsClientIdentityAndOwnershipFields(string field)
    {
        var json = "{\"firstName\":\"Alex\",\"lastName\":\"Chen\",\"" + field + "\":999}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateCaseContactRequestDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
