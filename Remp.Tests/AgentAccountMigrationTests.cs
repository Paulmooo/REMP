using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Remp.Common.Exceptions;
using Remp.Models.Entities;
using Remp.Repository.Interfaces;
using Remp.Repository.Repositories;
using Remp.Service.Services;

namespace Remp.Tests;

public class AgentAccountMigrationTests
{
    private static AgentAccountMigrationService Service(AgentCreationTransactionTests.Fixture f) =>
        new(new AgentAccountMigrationRepository(f.Db, f.Users));

    private static async Task<User> PrepareAsync(AgentCreationTransactionTests.Fixture f)
    {
        f.Db.Roles.AddRange(new Role { Name = "User", NormalizedName = "USER" }, new Role { Name = "Admin", NormalizedName = "ADMIN" });
        await f.Db.SaveChangesAsync();
        Assert.True((await f.Users.AddToRoleAsync((await f.Users.FindByIdAsync("company"))!, "Admin")).Succeeded);
        return await LegacyAsync(f, "mapped");
    }

    private static async Task<User> LegacyAsync(AgentCreationTransactionTests.Fixture f, string name)
    {
        var user = new User { UserName = name, Email = name + "@example.test", CreatedAt = DateTime.UtcNow.AddYears(-1) };
        Assert.True((await f.Users.CreateAsync(user, "Existing123!")).Succeeded);
        Assert.True((await f.Users.AddToRoleAsync(user, "User")).Succeeded);
        return user;
    }

    [Fact]
    public async Task ExplicitMapping_MigratesOnlyMappedAccountAndPreservesIdentityPasswordAndBusinessRows()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var mapped = await PrepareAsync(f);
        var unmapped = await LegacyAsync(f, "unmapped");
        var hash = mapped.PasswordHash; var createdAt = mapped.CreatedAt;
        var listing = new ListingCase { UserId = mapped.Id, User = mapped, Title = "Existing", Description = "Original", Street = "Street", City = "City", State = "NSW" };
        f.Db.ListingCases.Add(listing); await f.Db.SaveChangesAsync();
        AgentAccountMapping[] mappings = [new(mapped.Id, "company", "Given", "Family")];
        Assert.Equal(1, await Service(f).MigrateAsync(mappings)); // default validation only
        Assert.Empty(await f.Db.Agents.ToListAsync());
        Assert.True(await f.Users.IsInRoleAsync((await f.Users.FindByIdAsync(mapped.Id))!, "User"));
        Assert.Equal(1, await Service(f).MigrateAsync(mappings, apply: true));
        f.Db.ChangeTracker.Clear();
        var user = (await f.Users.FindByIdAsync(mapped.Id))!;
        Assert.Equal(hash, user.PasswordHash); Assert.Equal(createdAt, user.CreatedAt);
        Assert.Equal("mapped", user.UserName);
        Assert.True(await f.Users.CheckPasswordAsync(user, "Existing123!"));
        Assert.Equal(new[] { "Agent" }, await f.Users.GetRolesAsync(user));
        var profile = await f.Db.Agents.Include(a => a.PhotographyCompanies).SingleAsync();
        Assert.Equal(mapped.Id, profile.Id); Assert.Equal("Given", profile.AgentFirstName);
        Assert.Equal("Family", profile.AgentLastName); Assert.Equal("", profile.AvatarUrl);
        Assert.Equal("company", Assert.Single(profile.PhotographyCompanies).Id);
        Assert.Equal(mapped.Id, (await f.Db.ListingCases.SingleAsync()).UserId);
        Assert.Equal(listing.Id, (await f.Db.ListingCases.SingleAsync()).Id);
        Assert.Equal(new[] { "User" }, await f.Users.GetRolesAsync((await f.Users.FindByIdAsync(unmapped.Id))!));
        var me = await f.Service.FindCurrentUserInfoAsync(mapped.Id);
        Assert.Equal(new[] { "Agent" }, me.Roles);
        var login = await AccountWorkflowTests.Auth(f).LoginAsync(new Remp.Service.DTOs.Auth.LoginRequestDto
            { Email = mapped.Email!, Password = "Existing123!" });
        Assert.Equal(me.Roles, login.Roles);
        Assert.Empty(me.ListingCaseIds); // No inferred listing assignment or ownership transfer.
        await Assert.ThrowsAsync<ConflictException>(() => Service(f).MigrateAsync(mappings, apply: true));
        Assert.Equal(1, await f.Db.Agents.CountAsync());
    }

    [Theory]
    [InlineData("missing-user")]
    [InlineData("missing-company")]
    [InlineData("deleted")]
    [InlineData("admin-source")]
    [InlineData("inactive-company")]
    public async Task InvalidMapping_LeavesWholeBatchUnchanged(string scenario)
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var mapped = await PrepareAsync(f);
        var second = await LegacyAsync(f, "second");
        if (scenario == "deleted") { second.IsDeleted = true; await f.Users.UpdateAsync(second); }
        if (scenario == "admin-source") await f.Users.AddToRoleAsync(second, "Admin");
        if (scenario == "inactive-company") await f.Users.RemoveFromRoleAsync((await f.Users.FindByIdAsync("company"))!, "Admin");
        AgentAccountMapping[] mappings = [new(mapped.Id, "company", "First", "Agent"),
            new(scenario == "missing-user" ? "missing" : second.Id, scenario == "missing-company" ? "missing" : "company", "Second", "Agent")];
        if (scenario is "missing-user" or "missing-company")
            await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(f).MigrateAsync(mappings, apply: true));
        else
            await Assert.ThrowsAsync<ConflictException>(() => Service(f).MigrateAsync(mappings, apply: true));
        f.Db.ChangeTracker.Clear();
        Assert.Empty(await f.Db.Agents.ToListAsync());
        Assert.True(await f.Users.IsInRoleAsync((await f.Users.FindByIdAsync(mapped.Id))!, "User"));
        Assert.False(await f.Users.IsInRoleAsync((await f.Users.FindByIdAsync(mapped.Id))!, "Agent"));
    }

    [Fact]
    public async Task PersistenceFailure_RollsBackRoleChangesAndProfilesThenCanRetry()
    {
        var interceptor = new RejectProfileSave();
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync(interceptor);
        var user = await PrepareAsync(f);
        var second = await LegacyAsync(f, "second");
        AgentAccountMapping[] mappings = [new(user.Id, "company", "First", "Agent"), new(second.Id, "company", "Second", "Agent")];
        interceptor.Fail = true;
        await Assert.ThrowsAsync<DbUpdateException>(() => Service(f).MigrateAsync(mappings, apply: true));
        f.Db.ChangeTracker.Clear();
        Assert.Empty(await f.Db.Agents.ToListAsync());
        foreach (var id in new[] { user.Id, second.Id })
            Assert.Equal(new[] { "User" }, await f.Users.GetRolesAsync((await f.Users.FindByIdAsync(id))!));
        Assert.Equal(2, await Service(f).MigrateAsync(mappings, apply: true));
        Assert.Equal(2, await f.Db.Agents.CountAsync());
    }

    [Fact]
    public async Task DuplicateExistingEmail_RejectsMigrationWithoutChoosingAnAccount()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var user = await PrepareAsync(f);
        var other = await LegacyAsync(f, "other");
        other.NormalizedEmail = user.NormalizedEmail;
        await f.Db.SaveChangesAsync(); // Simulate conflicting legacy data; no unique email DB index exists.
        await Assert.ThrowsAsync<ConflictException>(() => Service(f).MigrateAsync([new(user.Id, "company", "First", "Last")], true));
        Assert.Empty(await f.Db.Agents.ToListAsync());
        Assert.Equal(new[] { "User" }, await f.Users.GetRolesAsync((await f.Users.FindByIdAsync(user.Id))!));
    }

    [Fact]
    public async Task IdentityRoleFailure_RollsBackAndKeepsPassword()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var user = await PrepareAsync(f); var hash = user.PasswordHash;
        f.Users.UserValidators.Clear(); f.Users.UserValidators.Add(new RejectValidation());
        await Assert.ThrowsAsync<ConflictException>(() => Service(f).MigrateAsync([new(user.Id, "company", "First", "Last")], apply: true));
        f.Db.ChangeTracker.Clear();
        Assert.Empty(await f.Db.Agents.ToListAsync());
        Assert.Equal(hash, (await f.Users.FindByIdAsync(user.Id))!.PasswordHash);
        Assert.Equal(new[] { "User" }, await f.Users.GetRolesAsync((await f.Users.FindByIdAsync(user.Id))!));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("duplicate")]
    [InlineData("avatar")]
    public async Task IncompleteMapping_IsRejectedBeforeDatabase(string scenario)
    {
        var repo = new Mock<IAgentAccountMigrationRepository>();
        AgentAccountMapping[] mappings = scenario switch
        {
            "duplicate" => [new("id", "company", "First", "Last"), new("id", "other", "First", "Last")],
            "avatar" => [new("id", "company", "First", "Last", "not-a-url")],
            _ => [new("id", "company", "", "Last")]
        };
        await Assert.ThrowsAsync<ArgumentException>(() => new AgentAccountMigrationService(repo.Object).MigrateAsync(mappings, true));
        repo.VerifyNoOtherCalls();
    }

    private sealed class RejectValidation : IUserValidator<User>
    {
        public Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user) =>
            Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "Rejected", Description = "Rejected" }));
    }

    private sealed class RejectProfileSave : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Fail && eventData.Context!.ChangeTracker.Entries<Agent>().Any(a => a.State == EntityState.Added))
            { Fail = false; throw new DbUpdateException("Injected profile failure"); }
            return ValueTask.FromResult(result);
        }
    }
}
