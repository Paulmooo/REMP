using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Remp.Common.Exceptions;
using Remp.Models.Entities;
using Remp.Models.Enums;
using Remp.Repository.Repositories;
using Remp.Service.DTOs.ListingCase;
using Remp.Service.Mapper;
using Remp.Service.Services;
using Remp.Service.Validators;

namespace Remp.Tests;

public class ListingContactTests
{
    private static ListingCaseService Service(AgentCreationTransactionTests.Fixture f) => new(
        new ListingCaseRepository(f.Db),
        new MapperConfiguration(c => c.AddProfile<MappingProfile>(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper(),
        new CreateListingCaseRequestValidator(), new UpdateListingCaseRequestValidator());

    private static async Task<(ListingCase Listing, string Agent)> Prepare(AgentCreationTransactionTests.Fixture f)
    {
        f.Db.Roles.Add(new Role { Name = "Admin", NormalizedName = "ADMIN" });
        await f.Db.SaveChangesAsync();
        Assert.True((await f.Users.AddToRoleAsync((await f.Users.FindByIdAsync("company"))!, "Admin")).Succeeded);
        var agent = await f.Service.CreateAgentAsync("company", UserServiceTests.ValidDto());
        var listing = new ListingCase
        {
            UserId = "company", Title = "Contact test", Description = "Test", Street = "1 Test Street", City = "Sydney", State = "NSW",
            Agents = [], MediaAssets = [], CaseContacts = [], CreatedAt = DateTime.UtcNow, ListingStatus = ListcaseStatus.Created
        };
        f.Db.Add(listing); await f.Db.SaveChangesAsync();
        await Service(f).AssignAgentAsync(listing.Id, agent.AgentId, "company");
        f.Db.ChangeTracker.Clear();
        return (listing, agent.AgentId);
    }

    private static CreateCaseContactRequestDto Valid() => new()
    {
        FirstName = " Alex ", LastName = " Chen ", CompanyName = "Independent",
        Email = "alex@example.test", PhoneNumber = "+61 (02) 1234-5678", ProfileUrl = "https://example.test/avatar.jpg"
    };

    [Fact]
    public async Task Empty_AddMultiple_ReadThroughListAndDetails_AndRevoke()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var (listing, agent) = await Prepare(f); var service = Service(f);
        Assert.Empty(await service.GetContactsAsync(listing.Id, agent));
        Assert.Empty(await service.GetContactsAsync(listing.Id, "company"));
        var other = new ListingCase { UserId = "company", Title = "Other", Description = "Test", Street = "Street", City = "Sydney", State = "NSW", Agents = [], CaseContacts = [], MediaAssets = [] };
        f.Db.Add(other); await f.Db.SaveChangesAsync();
        var first = await service.AddContactAsync(listing.Id, Valid(), agent);
        Assert.True(first.ContactId > 0); Assert.Equal("Alex", first.FirstName);
        Assert.Equal("Chen", first.LastName); Assert.Equal("Independent", first.CompanyName);
        Assert.Equal("alex@example.test", first.Email); Assert.Equal(Valid().PhoneNumber, first.PhoneNumber);
        Assert.Equal(Valid().ProfileUrl, first.ProfileUrl);
        var second = await service.AddContactAsync(listing.Id, new() { FirstName = "Sam", LastName = "Lee" }, agent);
        Assert.NotEqual(first.ContactId, second.ContactId); Assert.Equal("", second.Email);
        f.Db.ChangeTracker.Clear();
        foreach (var actor in new[] { "company", agent })
        {
            Assert.Equal(new[] { first.ContactId, second.ContactId }, (await service.GetContactsAsync(listing.Id, actor)).Select(c => c.ContactId));
            var details = await service.GetListingCaseDetailsAsync(listing.Id, actor, actor == agent ? "Agent" : "Admin");
            Assert.Equal(2, details.CaseContacts.Count);
        }
        Assert.Empty(await service.GetContactsAsync(other.Id, "company"));
        await service.UnassignAgentAsync(listing.Id, agent, "company");
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetContactsAsync(listing.Id, agent));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.AddContactAsync(listing.Id, Valid(), agent));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetListingCaseDetailsAsync(listing.Id, agent, "Agent"));
        Assert.Equal(2, await f.Db.CaseContacts.CountAsync());
        Assert.False(await f.Db.CaseContacts.AnyAsync(c => c.ListingCaseId == other.Id));
    }

    [Theory]
    [InlineData("first-empty")]
    [InlineData("last-empty")]
    [InlineData("first-long")]
    [InlineData("last-long")]
    [InlineData("company-long")]
    [InlineData("email")]
    [InlineData("email-long")]
    [InlineData("phone")]
    [InlineData("phone-long")]
    [InlineData("url")]
    [InlineData("url-relative")]
    [InlineData("url-credentials")]
    [InlineData("url-long")]
    [InlineData("null")]
    public async Task InvalidInput_DoesNotPersist(string scenario)
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var (listing, agent) = await Prepare(f); var dto = Valid();
        switch (scenario)
        {
            case "first-empty": dto.FirstName = "  "; break;
            case "last-empty": dto.LastName = null!; break;
            case "first-long": dto.FirstName = new string('a', 101); break;
            case "last-long": dto.LastName = new string('a', 101); break;
            case "company-long": dto.CompanyName = new string('a', 201); break;
            case "email": dto.Email = "invalid"; break;
            case "email-long": dto.Email = new string('a', 250) + "@test.test"; break;
            case "phone": dto.PhoneNumber = "call me"; break;
            case "phone-long": dto.PhoneNumber = new string('1', 33); break;
            case "url": dto.ProfileUrl = "javascript:alert(1)"; break;
            case "url-relative": dto.ProfileUrl = "/photo.jpg"; break;
            case "url-credentials": dto.ProfileUrl = "https://user:pass@example.test/a"; break;
            case "url-long": dto.ProfileUrl = "https://example.test/" + new string('a', 1000); break;
            case "null": dto = null!; break;
        }
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).AddContactAsync(listing.Id, dto, agent));
        Assert.Empty(await f.Db.CaseContacts.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrDeletedListing_RejectsReadAndWrite(bool deleted)
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var (listing, agent) = await Prepare(f);
        if (deleted) { (await f.Db.ListingCases.SingleAsync()).IsDeleted = true; await f.Db.SaveChangesAsync(); }
        var id = deleted ? listing.Id : 999;
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(f).GetContactsAsync(id, agent));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(f).AddContactAsync(id, Valid(), agent));
        Assert.Equal(0, await f.Db.CaseContacts.CountAsync());
    }

    [Fact]
    public async Task AdminCannotAdd_AndMissingIdentityAndInvalidIdsAreRejected()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var (listing, agent) = await Prepare(f); var service = Service(f);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.AddContactAsync(listing.Id, Valid(), "company"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetContactsAsync(listing.Id, ""));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AddContactAsync(listing.Id, Valid(), ""));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetContactsAsync(0, agent));
        await Assert.ThrowsAsync<ArgumentException>(() => service.AddContactAsync(-1, Valid(), agent));
        Assert.Equal(0, await f.Db.CaseContacts.CountAsync());
    }

    [Theory]
    [InlineData("foreign-company")]
    [InlineData("foreign-agent")]
    [InlineData("unassigned")]
    [InlineData("deleted-agent")]
    [InlineData("no-agent-role")]
    [InlineData("no-company-link")]
    [InlineData("deleted-admin")]
    [InlineData("locked-agent")]
    [InlineData("locked-admin")]
    [InlineData("no-admin-role")]
    [InlineData("no-admin-profile")]
    [InlineData("no-agent-profile")]
    public async Task Contacts_RejectInactiveOrUnrelatedActors(string scenario)
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var (listing, agent) = await Prepare(f);
        var actor = agent;
        if (scenario is "foreign-company" or "foreign-agent")
        {
            var user = new User { UserName = "other", Email = "other@example.test" };
            Assert.True((await f.Users.CreateAsync(user)).Succeeded);
            Assert.True((await f.Users.AddToRoleAsync(user, "Admin")).Succeeded);
            f.Db.PhotographyCompanies.Add(new PhotographyCompany { Id = user.Id, User = user, PhotographyCompanyName = "Other", Agents = [] });
            actor = user.Id;
            await f.Db.SaveChangesAsync();
            if (scenario == "foreign-agent")
            {
                var dto = UserServiceTests.ValidDto(); dto.Email = "foreign-agent@example.test";
                actor = (await f.Service.CreateAgentAsync(user.Id, dto)).AgentId;
                // Even a stale/corrupt cross-company assignment cannot grant access.
                (await f.Db.ListingCases.Include(l => l.Agents).SingleAsync()).Agents
                    .Add(await f.Db.Agents.SingleAsync(a => a.Id == actor));
            }
        }
        if (scenario == "unassigned") await Service(f).UnassignAgentAsync(listing.Id, agent, "company");
        if (scenario == "no-agent-role") await f.Users.RemoveFromRoleAsync((await f.Users.FindByIdAsync(agent))!, "Agent");
        if (scenario == "no-company-link")
            (await f.Db.Agents.Include(a => a.PhotographyCompanies).SingleAsync()).PhotographyCompanies.Clear();
        if (scenario is "deleted-agent" or "deleted-admin")
        {
            actor = scenario == "deleted-admin" ? "company" : agent;
            var user = (await f.Users.FindByIdAsync(actor))!; user.IsDeleted = true;
            Assert.True((await f.Users.UpdateAsync(user)).Succeeded);
        }
        if (scenario is "locked-agent" or "locked-admin")
        {
            actor = scenario == "locked-admin" ? "company" : agent;
            var user = (await f.Users.FindByIdAsync(actor))!;
            user.LockoutEnabled = true; user.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1);
            await f.Users.UpdateAsync(user);
        }
        if (scenario == "no-admin-role") { actor = "company"; await f.Users.RemoveFromRoleAsync((await f.Users.FindByIdAsync(actor))!, "Admin"); }
        if (scenario == "no-admin-profile") { actor = "company"; f.Db.PhotographyCompanies.Remove(await f.Db.PhotographyCompanies.SingleAsync()); }
        if (scenario == "no-agent-profile") f.Db.Agents.Remove(await f.Db.Agents.SingleAsync());
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(f).GetContactsAsync(listing.Id, actor));
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(f).AddContactAsync(listing.Id, Valid(), actor));
        await Assert.ThrowsAnyAsync<Exception>(() => Service(f).GetListingCaseDetailsAsync(listing.Id, actor, actor == "company" || scenario == "foreign-company" ? "Admin" : "Agent"));
        Assert.Equal(0, await f.Db.CaseContacts.CountAsync());
    }

    [Fact]
    public async Task StorageFailure_DoesNotLeaveRecord_AndContextCanRetry()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var (listing, agent) = await Prepare(f); var service = Service(f);
        await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_contact BEFORE INSERT ON CaseContacts BEGIN SELECT RAISE(ABORT, 'injected failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => service.AddContactAsync(listing.Id, Valid(), agent));
        Assert.Equal(0, await f.Db.CaseContacts.AsNoTracking().CountAsync());
        await f.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_contact");
        Assert.True((await service.AddContactAsync(listing.Id, Valid(), agent)).ContactId > 0);
        Assert.Equal(1, await f.Db.CaseContacts.CountAsync());
    }
}
