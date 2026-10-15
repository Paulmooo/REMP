using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Remp.API.Controllers;

namespace Remp.Tests;

public class AssignmentOpenApiTests
{
    [Fact]
    public async Task GeneratedDocument_ContainsAssignmentOperationsAndResponseContract()
    {
        // Standalone documentation host: does not run Program, seed hooks, SQL or Blob setup.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(ListingCaseController).Assembly);
        builder.Services.AddOpenApi();
        await using var app = builder.Build();
        app.MapControllers(); app.MapOpenApi();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
            var path = document.RootElement.GetProperty("paths").GetProperty("/api/ListingCase/listings/{id}/agents/{agentId}");
            foreach (var operation in new[] { "put", "delete" })
            {
                var responses = path.GetProperty(operation).GetProperty("responses");
                foreach (var status in new[] { "200", "400", "401", "403", "404" }) Assert.True(responses.TryGetProperty(status, out _));
            }
            var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
            var assignment = schemas.GetProperty("ListingAgentAssignmentDto").GetProperty("properties");
            foreach (var field in new[] { "listingCaseId", "agentId", "assigned" }) Assert.True(assignment.TryGetProperty(field, out _));
        }
        finally { await app.StopAsync(); }
    }
}
