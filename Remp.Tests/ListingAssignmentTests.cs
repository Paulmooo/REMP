using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Remp.Common.Exceptions;
using Remp.Models.Entities;
using Remp.Repository.Repositories;
using Remp.Service.Mapper;
using Remp.Service.Services;
using Remp.Service.Validators;

namespace Remp.Tests;

public class ListingAssignmentTests
{
    // Test-only records in the existing isolated SQLite fixture; no application initialization.
    private static async Task PrepareAssignments(AgentCreationTransactionTests.Fixture f)
    {
        f.Db.Roles.AddRange(new Role { Name = "Admin", NormalizedName = "ADMIN" },
            new Role { Name = "User", NormalizedName = "USER" });
        await f.Db.SaveChangesAsync();
        Assert.True((await f.Users.AddToRoleAsync((await f.Users.FindByIdAsync("company"))!, "User")).Succeeded);
        foreach (var suffix in new[] { "a", "b" })
        {
            var email = $"company-{suffix}@assignment.example.test";
            var user = new User { UserName = email, Email = email, CreatedAt = DateTime.UtcNow };
            Assert.True((await f.Users.CreateAsync(user)).Succeeded);
            Assert.True((await f.Users.AddToRoleAsync(user, "Admin")).Succeeded);
            f.Db.PhotographyCompanies.Add(new PhotographyCompany
            {
                Id = user.Id, User = user, PhotographyCompanyName = $"Company {suffix}", Agents = []
            });
            await f.Db.SaveChangesAsync();
            var dto = UserServiceTests.ValidDto(); dto.Email = $"agent-{suffix}@assignment.example.test";
            await f.Service.CreateAgentAsync(user.Id, dto);
        }
        f.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Assignment_RemovesStaleRelationAndPreservesOtherAgentsOnSameListing()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync(); await PrepareAssignments(f);
        var owner = (await f.Users.FindByEmailAsync("company-a@assignment.example.test"))!.Id;
        var agent = (await f.Users.FindByEmailAsync("agent-a@assignment.example.test"))!;
        var dto = UserServiceTests.ValidDto(); dto.Email = "second@assignment.example.test";
        var second = await f.Service.CreateAgentAsync(owner, dto);
        var listing = Listing(owner); f.Db.Add(listing); await f.Db.SaveChangesAsync();
        var service = Listings(f);
        await service.AssignAgentAsync(listing.Id, agent.Id, owner);
        await service.AssignAgentAsync(listing.Id, second.AgentId, owner);
        var profile = await f.Db.Agents.Include(a => a.PhotographyCompanies).SingleAsync(a => a.Id == agent.Id);
        profile.PhotographyCompanies.Clear(); agent.IsDeleted = true;
        await f.Db.SaveChangesAsync();
        await service.UnassignAgentAsync(listing.Id, agent.Id, owner);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(second.AgentId, Assert.Single((await f.Db.ListingCases.Include(l => l.Agents).SingleAsync()).Agents).Id);
        listing = (await f.Db.ListingCases.SingleAsync()); listing.IsDeleted = true; await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UnassignAgentAsync(listing.Id, second.AgentId, owner));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AssignAgentAsync(listing.Id, second.AgentId, owner));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AssignAgentAsync(listing.Id, second.AgentId, ""));
    }

    private static ListingCaseService Listings(AgentCreationTransactionTests.Fixture f) => new(
        new ListingCaseRepository(f.Db),
        new MapperConfiguration(c => c.AddProfile<MappingProfile>(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper(),
        new CreateListingCaseRequestValidator(), new UpdateListingCaseRequestValidator());

    private static ListingCase Listing(string owner) => new()
    {
        UserId = owner, Title = "Assignment test listing", Description = "Test", Street = "1 Test Street", City = "Sydney", State = "NSW",
        Agents = [], MediaAssets = [], CaseContacts = [], CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Assignment_IsIdempotentAndRevokesOnlyTargetListingAcrossFreshReads()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync(); await PrepareAssignments(f);
        var owner = (await f.Users.FindByEmailAsync("company-a@assignment.example.test"))!.Id;
        var other = (await f.Users.FindByEmailAsync("company-b@assignment.example.test"))!.Id;
        var agent = (await f.Users.FindByEmailAsync("agent-a@assignment.example.test"))!.Id;
        var agentB = (await f.Users.FindByEmailAsync("agent-b@assignment.example.test"))!.Id;
        var first = Listing(owner); var second = Listing(owner); var third = Listing(other);
        f.Db.AddRange(first, second, third); await f.Db.SaveChangesAsync();
        var service = Listings(f);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetListingCaseDetailsAsync(first.Id, agent, "Agent"));
        Assert.True((await service.AssignAgentAsync(first.Id, agent, owner)).Assigned);
        await service.AssignAgentAsync(first.Id, agent, owner);
        await service.AssignAgentAsync(second.Id, agent, owner); await service.AssignAgentAsync(third.Id, agentB, other);
        f.Db.ChangeTracker.Clear();
        Assert.Single((await f.Db.ListingCases.Include(l => l.Agents).SingleAsync(l => l.Id == first.Id)).Agents);
        Assert.Equal(first.Id, (await service.GetListingCaseDetailsAsync(first.Id, agent, "Agent")).Id);
        Assert.Equal(2, (await service.GetAllListingCasesAsync(1, 10, agent, "Agent")).TotalCount);
        Assert.Empty(await service.GetListingCaseMediaAssetsAsync(first.Id, agent, "Agent"));
        Assert.Contains(first.Id, (await f.Service.FindCurrentUserInfoAsync(agent)).ListingCaseIds);
        Assert.False((await service.UnassignAgentAsync(first.Id, agent, owner)).Assigned);
        await service.UnassignAgentAsync(first.Id, agent, owner);
        await service.UnassignAgentAsync(first.Id, "nonexistent-agent", owner);
        f.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetListingCaseDetailsAsync(first.Id, agent, "Agent"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetListingCaseMediaAssetsAsync(first.Id, agent, "Agent"));
        Assert.DoesNotContain(first.Id, (await f.Service.FindCurrentUserInfoAsync(agent)).ListingCaseIds);
        Assert.Equal(1, (await service.GetAllListingCasesAsync(1, 10, agent, "Agent")).TotalCount);
        Assert.Equal(second.Id, (await service.GetListingCaseDetailsAsync(second.Id, agent, "Agent")).Id);
        Assert.Equal(third.Id, (await service.GetListingCaseDetailsAsync(third.Id, agentB, "Agent")).Id);
    }

    [Theory]
    [InlineData("other-company")]
    [InlineData("agent")]
    [InlineData("ordinary-user")]
    [InlineData("deleted-admin")]
    [InlineData("admin-without-role")]
    public async Task Assignment_UnauthorizedActorsCannotAssignOrRemove(string scenario)
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync(); await PrepareAssignments(f);
        var owner = (await f.Users.FindByEmailAsync("company-a@assignment.example.test"))!;
        var agent = (await f.Users.FindByEmailAsync("agent-a@assignment.example.test"))!.Id;
        var listing = Listing(owner.Id); f.Db.Add(listing); await f.Db.SaveChangesAsync();
        var service = Listings(f); await service.AssignAgentAsync(listing.Id, agent, owner.Id);
        var actor = scenario switch
        {
            "other-company" => (await f.Users.FindByEmailAsync("company-b@assignment.example.test"))!.Id,
            "agent" => agent, "ordinary-user" => "company", _ => owner.Id
        };
        if (scenario == "deleted-admin") { owner.IsDeleted = true; await f.Users.UpdateAsync(owner); }
        if (scenario == "admin-without-role") await f.Users.RemoveFromRoleAsync(owner, "Admin");
        await Assert.ThrowsAsync<ForbiddenException>(() => service.AssignAgentAsync(listing.Id, agent, actor));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.UnassignAgentAsync(listing.Id, agent, actor));
        f.Db.ChangeTracker.Clear(); Assert.Single((await f.Db.ListingCases.Include(l => l.Agents).SingleAsync()).Agents);
    }

    [Theory]
    [InlineData("foreign", typeof(ForbiddenException))]
    [InlineData("deleted", typeof(KeyNotFoundException))]
    [InlineData("no-role", typeof(KeyNotFoundException))]
    [InlineData("missing", typeof(KeyNotFoundException))]
    [InlineData("", typeof(ArgumentException))]
    public async Task Assignment_RejectsInvalidTargets(string scenario, Type error)
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync(); await PrepareAssignments(f);
        var owner = (await f.Users.FindByEmailAsync("company-a@assignment.example.test"))!.Id;
        var agent = (await f.Users.FindByEmailAsync($"agent-{(scenario == "foreign" ? "b" : "a")}@assignment.example.test"))!;
        var listing = Listing(owner); f.Db.Add(listing); await f.Db.SaveChangesAsync();
        if (scenario == "deleted") { agent.IsDeleted = true; await f.Users.UpdateAsync(agent); }
        if (scenario == "no-role") await f.Users.RemoveFromRoleAsync(agent, "Agent");
        var id = scenario == "missing" ? "missing" : scenario == "" ? "" : agent.Id;
        await Assert.ThrowsAsync(error, () => Listings(f).AssignAgentAsync(listing.Id, id, owner));
        f.Db.ChangeTracker.Clear(); Assert.Empty((await f.Db.ListingCases.Include(l => l.Agents).SingleAsync()).Agents);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Listings(f).AssignAgentAsync(999, agent.Id, owner));
    }
}
