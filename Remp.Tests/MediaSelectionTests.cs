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

public class MediaSelectionTests
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
            UserId = "company", Title = "Selection test", Description = "Test", Street = "1 Test Street", City = "Sydney", State = "NSW",
            Agents = [], MediaAssets = [], CaseContacts = [], CreatedAt = DateTime.UtcNow, ListingStatus = ListcaseStatus.Created
        };
        f.Db.Add(listing); await f.Db.SaveChangesAsync();
        await Service(f).AssignAgentAsync(listing.Id, agent.AgentId, "company");
        for (var i = 0; i < 12; i++)
            f.Db.MediaAssets.Add(new MediaAsset
            {
                ListingCaseId = listing.Id, UserId = "company", MediaType = MediaType.Picture,
                MediaUrl = $"https://media.example.test/{i}.jpg", UploadedAt = DateTime.UtcNow
            });
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        return (listing, agent.AgentId);
    }

    [Fact]
    public async Task Read_RestoresFlagsAndSharedSelection_ThenRevokesAfterUnassignment()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var (listing, agent) = await Prepare(f);
        var media = await f.Db.MediaAssets.OrderBy(m => m.Id).ToListAsync();
        media[0].IsSelected = true; media[0].IsHero = true;
        media[1].IsSelected = true; media[1].IsDeleted = true;
        media[2].IsSelected = true; media[2].MediaType = MediaType.Video;
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var service = Service(f);
        foreach (var actor in new[] { "company", agent })
        {
            var result = await service.GetFinalSelectionAsync(listing.Id, actor);
            Assert.Equal(listing.Id, result.ListingCaseId);
            Assert.Equal(media[0].Id, Assert.Single(result.Pictures).Id);
            Assert.True(result.Pictures[0].IsSelected); Assert.True(result.Pictures[0].IsHero);
            Assert.Equal(media[0].Id, result.Hero!.Id);
        }
        var grouped = await service.GetListingCaseMediaAssetsAsync(listing.Id, agent, "Agent");
        Assert.True(grouped.SelectMany(g => g.Items).Single(m => m.Id == media[0].Id).IsHero);
        var detail = await service.GetListingCaseDetailsAsync(listing.Id, agent, "Agent");
        Assert.True(detail.MediaAssets.Single(m => m.Id == media[0].Id).IsSelected);
        await service.UnassignAgentAsync(listing.Id, agent, "company");
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetFinalSelectionAsync(listing.Id, agent));
    }

    [Fact]
    public async Task Read_EmptySelectionAndInvalidIdentity()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var (listing, agent) = await Prepare(f); var service = Service(f);
        var result = await service.GetFinalSelectionAsync(listing.Id, agent);
        Assert.Empty(result.Pictures); Assert.Null(result.Hero);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetFinalSelectionAsync(listing.Id, ""));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetFinalSelectionAsync(0, agent));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetFinalSelectionAsync(listing.Id, "missing"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetFinalSelectionAsync(999, agent));
    }

    [Theory]
    [InlineData("foreign-company")]
    [InlineData("foreign-agent")]
    [InlineData("unassigned")]
    [InlineData("deleted-agent")]
    [InlineData("no-agent-role")]
    [InlineData("no-company-link")]
    [InlineData("deleted-admin")]
    public async Task Read_RejectsInactiveOrUnrelatedActors(string scenario)
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
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(f).GetFinalSelectionAsync(listing.Id, actor));
        var ids = await Ids(f); var before = await Flags(f);
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(f).SaveMediaSelectionAsync(listing.Id, Selection([ids[0]], ids[0]), actor));
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(f).SetHeroAsync(listing.Id, new() { MediaId = ids[0] }, actor));
        Assert.Equal(before, await Flags(f));
    }

    [Fact]
    public async Task AssignedAgents_ShareOneSelection_AndDeliveredCanStillBeRead()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var (listing, first) = await Prepare(f); var ids = await Ids(f); var service = Service(f);
        var dto = UserServiceTests.ValidDto(); dto.Email = "second-agent@example.test";
        var second = (await f.Service.CreateAgentAsync("company", dto)).AgentId;
        await service.AssignAgentAsync(listing.Id, second, "company");
        await service.SaveMediaSelectionAsync(listing.Id, Selection([ids[0], ids[1]], ids[0]), first);
        Assert.Equal(2, (await service.GetFinalSelectionAsync(listing.Id, second)).Pictures.Count);
        await service.SetHeroAsync(listing.Id, new() { MediaId = ids[1] }, second);
        Assert.Equal(ids[1], (await service.GetFinalSelectionAsync(listing.Id, first)).Hero!.Id);
        (await f.Db.ListingCases.SingleAsync()).ListingStatus = ListcaseStatus.Delivered;
        await f.Db.SaveChangesAsync();
        foreach (var actor in new[] { first, second, "company" })
            Assert.Equal(ids[1], (await service.GetFinalSelectionAsync(listing.Id, actor)).Hero!.Id);
    }
    private static SaveMediaSelectionRequestDto Selection(int[] ids, int? hero = null) => new() { MediaIds = ids.ToList(), HeroMediaId = hero };

    private static async Task<int[]> Ids(AgentCreationTransactionTests.Fixture f) =>
        await f.Db.MediaAssets.AsNoTracking().OrderBy(m => m.Id).Select(m => m.Id).ToArrayAsync();

    private static async Task<string[]> Flags(AgentCreationTransactionTests.Fixture f) =>
        (await f.Db.MediaAssets.AsNoTracking().OrderBy(m => m.Id).ToListAsync())
            .Select(m => $"{m.Id}:{m.IsSelected}:{m.IsHero}").ToArray();

    [Fact]
    public async Task Save_ReplacesCompleteSelection_ChangesCover_RepeatsAndClears()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var (listing, agent) = await Prepare(f); var ids = await Ids(f); var service = Service(f);
        var request = Selection(ids.Take(10).Reverse().ToArray(), ids[0]);
        for (var i = 0; i < 2; i++)
        {
            var result = await service.SaveMediaSelectionAsync(listing.Id, request, agent);
            Assert.Equal(ids.Take(10), result.Pictures.Select(m => m.Id));
            Assert.Equal(ids[0], result.Hero!.Id);
            Assert.Equal(10, await f.Db.MediaAssets.CountAsync(m => m.IsSelected));
            Assert.Equal(1, await f.Db.MediaAssets.CountAsync(m => m.IsHero));
        }
        for (var i = 0; i < 2; i++)
        {
            var changed = await service.SetHeroAsync(listing.Id, new() { MediaId = ids[1] }, agent);
            Assert.Equal(ids[1], changed.Hero!.Id);
            Assert.Equal(1, await f.Db.MediaAssets.CountAsync(m => m.IsHero));
        }
        var reduced = await service.SaveMediaSelectionAsync(listing.Id, Selection([ids[2]], ids[2]), agent);
        Assert.Equal(ids[2], Assert.Single(reduced.Pictures).Id);
        Assert.Equal(ids[2], (await service.GetFinalSelectionAsync(listing.Id, "company")).Hero!.Id);
        var noHero = await service.SaveMediaSelectionAsync(listing.Id, Selection([ids[2]]), agent);
        Assert.Null(noHero.Hero); Assert.Single(noHero.Pictures);
        var cleared = await service.SaveMediaSelectionAsync(listing.Id, Selection([]), agent);
        Assert.Empty(cleared.Pictures); Assert.Null(cleared.Hero);
        Assert.False(await f.Db.MediaAssets.AnyAsync(m => m.IsSelected || m.IsHero));
    }

    [Theory]
    [InlineData("eleven")]
    [InlineData("duplicate")]
    [InlineData("foreign")]
    [InlineData("video")]
    [InlineData("deleted")]
    [InlineData("missing")]
    [InlineData("negative")]
    [InlineData("hero-outside-selection")]
    [InlineData("null-ids")]
    [InlineData("delivered")]
    public async Task Save_InvalidReplacementLeavesEveryFlagUnchanged(string scenario)
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var (listing, agent) = await Prepare(f); var ids = await Ids(f); var service = Service(f);
        await service.SaveMediaSelectionAsync(listing.Id, Selection([ids[0], ids[1]], ids[0]), agent);
        var request = Selection([ids[2], ids[3]], ids[2]);
        switch (scenario)
        {
            case "eleven": request = Selection(ids.Take(11).ToArray(), ids[0]); break;
            case "duplicate": request = Selection([ids[2], ids[2]], ids[2]); break;
            case "foreign":
                var other = new ListingCase { UserId = "company", Title = "Other", Description = "Test", Street = "Street", City = "Sydney", State = "NSW", Agents = [], CaseContacts = [], MediaAssets = [] };
                f.Db.Add(other); await f.Db.SaveChangesAsync();
                (await f.Db.MediaAssets.SingleAsync(m => m.Id == ids[3])).ListingCaseId = other.Id;
                break;
            case "video": (await f.Db.MediaAssets.SingleAsync(m => m.Id == ids[3])).MediaType = MediaType.Video; break;
            case "deleted": (await f.Db.MediaAssets.SingleAsync(m => m.Id == ids[3])).IsDeleted = true; break;
            case "missing": request = Selection([ids[2], 99999], ids[2]); break;
            case "negative": request = Selection([-1]); break;
            case "hero-outside-selection": request.HeroMediaId = ids[0]; break;
            case "null-ids": request.MediaIds = null!; break;
            case "delivered": (await f.Db.ListingCases.SingleAsync()).ListingStatus = ListcaseStatus.Delivered; break;
        }
        await f.Db.SaveChangesAsync();
        var before = await Flags(f);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveMediaSelectionAsync(listing.Id, request, agent));
        Assert.Equal(before, await Flags(f));
    }

    [Theory]
    [InlineData("unselected")]
    [InlineData("deleted")]
    [InlineData("video")]
    [InlineData("foreign")]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("negative")]
    [InlineData("delivered")]
    public async Task Hero_InvalidChangeLeavesSelectionAndCoverUnchanged(string scenario)
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var (listing, agent) = await Prepare(f); var ids = await Ids(f); var service = Service(f);
        await service.SaveMediaSelectionAsync(listing.Id, Selection([ids[0], ids[1]], ids[0]), agent);
        int? target = ids[1];
        switch (scenario)
        {
            case "unselected": target = ids[2]; break;
            case "deleted": (await f.Db.MediaAssets.SingleAsync(m => m.Id == ids[1])).IsDeleted = true; break;
            case "video": (await f.Db.MediaAssets.SingleAsync(m => m.Id == ids[1])).MediaType = MediaType.Video; break;
            case "foreign":
                var other = new ListingCase { UserId = "company", Title = "Other", Description = "Test", Street = "Street", City = "Sydney", State = "NSW", Agents = [], CaseContacts = [], MediaAssets = [] };
                f.Db.Add(other); await f.Db.SaveChangesAsync();
                (await f.Db.MediaAssets.SingleAsync(m => m.Id == ids[1])).ListingCaseId = other.Id;
                break;
            case "missing": target = 99999; break;
            case "null": target = null; break;
            case "negative": target = -1; break;
            case "delivered": (await f.Db.ListingCases.SingleAsync()).ListingStatus = ListcaseStatus.Delivered; break;
        }
        await f.Db.SaveChangesAsync(); var before = await Flags(f);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetHeroAsync(listing.Id, new() { MediaId = target }, agent));
        Assert.Equal(before, await Flags(f));
    }

    [Fact]
    public async Task Save_StorageFailureRollsBackAndSameContextCanRetry()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var (listing, agent) = await Prepare(f); var ids = await Ids(f); var service = Service(f);
        await service.SaveMediaSelectionAsync(listing.Id, Selection([ids[0]], ids[0]), agent);
        var before = await Flags(f);
        await f.Db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_hero BEFORE UPDATE ON MediaAssets WHEN NEW.IsHero = 1
            BEGIN SELECT RAISE(ABORT, 'injected media update failure'); END;
            """);
        await Assert.ThrowsAnyAsync<Exception>(() => service.SaveMediaSelectionAsync(listing.Id, Selection([ids[1]], ids[1]), agent));
        Assert.Equal(before, await Flags(f));
        await Assert.ThrowsAnyAsync<Exception>(() => service.SetHeroAsync(listing.Id, new() { MediaId = ids[0] }, agent));
        Assert.Equal(before, await Flags(f));
        await f.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_hero");
        Assert.Equal(ids[1], (await service.SaveMediaSelectionAsync(listing.Id, Selection([ids[1]], ids[1]), agent)).Hero!.Id);
    }

    [Fact]
    public async Task Writes_RejectAdminMissingIdentityMissingListingAndRevokedAssignment()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var (listing, agent) = await Prepare(f); var ids = await Ids(f); var service = Service(f);
        var dto = Selection([ids[0]], ids[0]);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.SaveMediaSelectionAsync(listing.Id, dto, "company"));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.SetHeroAsync(listing.Id, new() { MediaId = ids[0] }, "company"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveMediaSelectionAsync(listing.Id, dto, ""));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SetHeroAsync(listing.Id, new() { MediaId = ids[0] }, ""));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SaveMediaSelectionAsync(999, dto, agent));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetHeroAsync(999, new() { MediaId = ids[0] }, agent));
        await service.SaveMediaSelectionAsync(listing.Id, dto, agent);
        var before = await Flags(f);
        await service.UnassignAgentAsync(listing.Id, agent, "company");
        await Assert.ThrowsAsync<ForbiddenException>(() => service.SaveMediaSelectionAsync(listing.Id, Selection([]), agent));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.SetHeroAsync(listing.Id, new() { MediaId = ids[0] }, agent));
        Assert.Equal(before, await Flags(f));
    }

    [Fact]
    public async Task DeletedListing_RejectsReadsAndBothWritesWithoutChanges()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var (listing, agent) = await Prepare(f); var ids = await Ids(f); var service = Service(f);
        await service.SaveMediaSelectionAsync(listing.Id, Selection([ids[0]], ids[0]), agent);
        (await f.Db.ListingCases.SingleAsync()).IsDeleted = true;
        await f.Db.SaveChangesAsync(); var before = await Flags(f);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetFinalSelectionAsync(listing.Id, agent));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SaveMediaSelectionAsync(listing.Id, Selection([]), agent));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetHeroAsync(listing.Id, new() { MediaId = ids[0] }, agent));
        Assert.Equal(before, await Flags(f));
    }
}
