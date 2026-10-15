using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Remp.Common.Exceptions;
using Remp.Models.Entities;
using Remp.Repository.Repositories;
using Remp.Service.DTOs.Auth;
using Remp.Service.DTOs.User;
using Remp.Service.Services;

namespace Remp.Tests;

public class AccountWorkflowTests
{
    private const string Key = "test-signing-key-at-least-thirty-two-bytes-long";
    internal static AuthService Auth(AgentCreationTransactionTests.Fixture f) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = Key, ["Jwt:Issuer"] = "tests", ["Jwt:Audience"] = "tests-client"
        }).Build(), new AuthRepository(f.Db, f.Users), new MapperConfiguration(c => c.AddProfile<Remp.Service.Mapper.MappingProfile>(),
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper());

    [Fact]
    public async Task CreatedAgent_LoginJwtMeAndPasswordChange_WorkTogether()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var created = await f.Service.CreateAgentAsync("company", UserServiceTests.ValidDto());
        await f.Users.AddPasswordAsync((await f.Users.FindByIdAsync(created.AgentId))!, "Valid123!");
        var auth = Auth(f);
        var login = await auth.LoginAsync(new LoginRequestDto { Email = created.Email, Password = "Valid123!" });
        Assert.Equal(new[] { "Agent" }, login.Roles);
        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(login.Token, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = "tests", ValidateAudience = true, ValidAudience = "tests-client",
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key))
        }, out var validated);
        Assert.Equal(created.AgentId, principal.FindFirstValue("uid"));
        Assert.All(principal.FindAll(ClaimTypes.NameIdentifier), c => Assert.Equal(created.AgentId, c.Value));
        Assert.True(principal.IsInRole("Agent"));
        Assert.InRange(validated.ValidTo - DateTime.UtcNow, TimeSpan.FromMinutes(59), TimeSpan.FromMinutes(61));
        var me = await f.Service.FindCurrentUserInfoAsync(created.AgentId);
        Assert.Equal(login.Roles, me.Roles);
        Assert.Empty(me.ListingCaseIds);
        Assert.DoesNotContain("temporaryPassword", JsonSerializer.Serialize(created, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var user = (await f.Users.FindByIdAsync(created.AgentId))!;
        var oldHash = user.PasswordHash;
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.UpdatePasswordAsync(user.Id,
            new UpdatePasswordRequestDto { CurrentPassword = "Wrong123!", NewPassword = "NewValid123!" }));
        Assert.Equal(oldHash, user.PasswordHash);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.UpdatePasswordAsync(user.Id,
            new UpdatePasswordRequestDto { CurrentPassword = "Valid123!", NewPassword = "onlylowercase" }));
        Assert.True(await f.Users.CheckPasswordAsync(user, "Valid123!"));
        await f.Service.UpdatePasswordAsync(user.Id, new UpdatePasswordRequestDto
            { CurrentPassword = "Valid123!", NewPassword = "NewValid123!" });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => auth.LoginAsync(new LoginRequestDto
            { Email = created.Email, Password = "Valid123!" }));
        Assert.NotEmpty((await auth.LoginAsync(new LoginRequestDto { Email = created.Email, Password = "NewValid123!" })).Token);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-password")]
    [InlineData("deleted")]
    [InlineData("locked")]
    public async Task Login_InvalidAccounts_UseSameFailure(string scenario)
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var created = await f.Service.CreateAgentAsync("company", UserServiceTests.ValidDto());
        var user = (await f.Users.FindByIdAsync(created.AgentId))!;
        await f.Users.AddPasswordAsync(user, "Valid123!");
        if (scenario == "deleted") { user.IsDeleted = true; await f.Users.UpdateAsync(user); }
        if (scenario == "locked") { await f.Users.SetLockoutEnabledAsync(user, true); await f.Users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddDays(1)); }
        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Auth(f).LoginAsync(new LoginRequestDto
        {
            Email = scenario == "missing" ? "missing@example.test" : created.Email,
            Password = scenario == "wrong-password" ? "Wrong123!" : "Valid123!"
        }));
        Assert.Equal("Invalid login credentials.", error.Message);
    }

    [Theory]
    [InlineData("Agent")]
    [InlineData("Admin")]
    public async Task RoleWithoutProfile_CannotLoginOrReadMe(string role)
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        if (role == "Admin") { f.Db.Roles.Add(new Role { Name = role, NormalizedName = role.ToUpperInvariant() }); await f.Db.SaveChangesAsync(); }
        var user = new User { UserName = "orphan", Email = "orphan@example.test" };
        Assert.True((await f.Users.CreateAsync(user, "Valid123!")).Succeeded);
        Assert.True((await f.Users.AddToRoleAsync(user, role)).Succeeded);
        await Assert.ThrowsAsync<ForbiddenException>(() => Auth(f).LoginAsync(new LoginRequestDto { Email = "orphan@example.test", Password = "Valid123!" }));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.FindCurrentUserInfoAsync(user.Id));
    }

    [Fact]
    public async Task Search_IsNormalizedExactAndRejectsNonAgentAndDeletedAccounts()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var created = await f.Service.CreateAgentAsync("company", UserServiceTests.ValidDto());
        Assert.Equal(created.AgentId, (await f.Service.GetAgentByEmailAsync("company", "AGENT@EXAMPLE.TEST")).Id);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GetAgentByEmailAsync("company", "agent"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Service.GetAgentByEmailAsync("company", "otheragent@example.test"));
        var user = (await f.Users.FindByIdAsync(created.AgentId))!;
        await f.Users.RemoveFromRoleAsync(user, "Agent");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Service.GetAgentByEmailAsync("company", created.Email));
        await f.Users.AddToRoleAsync(user, "Agent");
        user.IsDeleted = true; await f.Users.UpdateAsync(user);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Service.GetAgentByEmailAsync("company", created.Email));
    }

    [Fact]
    public async Task DuplicateEmail_WithDifferentUsername_CannotCreateAgent()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        Assert.True((await f.Users.CreateAsync(new User { UserName = "legacy", Email = "AGENT@EXAMPLE.TEST" }, "Valid123!")).Succeeded);
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.CreateAgentAsync("company", UserServiceTests.ValidDto()));
        await f.AssertCountsAsync(2, 0, 0, 0);
    }

    [Fact]
    public async Task CompanyAgents_ContainOnlyOwnActiveAgentAccounts()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var own = await f.Service.CreateAgentAsync("company", UserServiceTests.ValidDto());
        var second = new User { UserName = "company-b", Email = "b@example.test" };
        Assert.True((await f.Users.CreateAsync(second)).Succeeded);
        f.Db.PhotographyCompanies.Add(new PhotographyCompany { Id = second.Id, User = second, PhotographyCompanyName = "B", Agents = [] });
        await f.Db.SaveChangesAsync();
        var dto = UserServiceTests.ValidDto(); dto.Email = "other@example.test";
        var other = await f.Service.CreateAgentAsync(second.Id, dto);
        f.Db.ChangeTracker.Clear();
        var repo = new UserRepository(f.Db, f.Users);
        Assert.Equal(own.AgentId, Assert.Single(await f.Service.GetAgentsByCompanyIdAsync("company")).Id);
        Assert.Equal(other.AgentId, Assert.Single(await f.Service.GetAgentsByCompanyIdAsync(second.Id)).Id);
        var user = (await f.Users.FindByIdAsync(own.AgentId))!;
        await f.Users.RemoveFromRoleAsync(user, "Agent");
        Assert.Empty(await f.Service.GetAgentsByCompanyIdAsync("company"));
        await f.Users.AddToRoleAsync(user, "Agent");
        user.IsDeleted = true; await f.Users.UpdateAsync(user);
        f.Db.ChangeTracker.Clear();
        Assert.Empty(await f.Service.GetAgentsByCompanyIdAsync("company"));
    }

    [Fact]
    public async Task MissingCompany_CannotSearchAgents()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.GetAgentByEmailAsync("missing", "agent@example.test"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetAgentByEmailAsync("", "agent@example.test"));
    }

    [Fact]
    public async Task DeletedAccount_CannotReadMeOrChangePassword()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var created = await f.Service.CreateAgentAsync("company", UserServiceTests.ValidDto());
        var user = (await f.Users.FindByIdAsync(created.AgentId))!;
        await f.Users.AddPasswordAsync(user, "Valid123!");
        user.IsDeleted = true; await f.Users.UpdateAsync(user);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.FindCurrentUserInfoAsync(user.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.UpdatePasswordAsync(user.Id,
            new UpdatePasswordRequestDto { CurrentPassword = "Valid123!", NewPassword = "NewValid123!" }));
        Assert.True(await f.Users.CheckPasswordAsync(user, "Valid123!"));
    }

    [Fact]
    public async Task RegistrationRoleFailure_RollsBackAndAllowsRetryInSameScope()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var repo = new AuthRepository(f.Db, f.Users);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.RegisterWithRoleAsync(
            new User { UserName = "retry", Email = "retry@example.test" }, "Valid123!", "MissingRole"));
        await f.AssertCountsAsync(1, 0, 0, 0);
        Assert.Empty(f.Db.ChangeTracker.Entries());
        Assert.True((await repo.RegisterWithRoleAsync(new User { UserName = "retry", Email = "retry@example.test" }, "Valid123!", "Agent")).Succeeded);
        Assert.True(await f.Users.IsInRoleAsync((await repo.FindByUsernameAsync("retry"))!, "Agent"));
    }

    [Fact]
    public async Task PendingAgent_CannotLoginAndDoesNotHaveOrReturnPassword()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var created = await f.Service.CreateAgentAsync("company", UserServiceTests.ValidDto());
        Assert.True(created.RequiresInvitation);
        Assert.False(await f.Users.HasPasswordAsync((await f.Users.FindByIdAsync(created.AgentId))!));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Auth(f).LoginAsync(new LoginRequestDto
            { Email = created.Email, Password = "Valid123!" }));
        Assert.DoesNotContain("password", JsonSerializer.Serialize(created, new JsonSerializerOptions(JsonSerializerDefaults.Web)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmailLogin_DoesNotFallbackToUsername()
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        var created = await f.Service.CreateAgentAsync("company", UserServiceTests.ValidDto());
        var user = (await f.Users.FindByIdAsync(created.AgentId))!;
        await f.Users.SetUserNameAsync(user, "old@example.test");
        await f.Users.AddPasswordAsync(user, "Valid123!");
        Assert.NotEmpty((await Auth(f).LoginAsync(new LoginRequestDto { Email = "AGENT@EXAMPLE.TEST", Password = "Valid123!" })).Token);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Auth(f).LoginAsync(new LoginRequestDto { Email = "old@example.test", Password = "Valid123!" }));
        var legacy = JsonSerializer.Deserialize<LoginRequestDto>("{\"userName\":\"agent@example.test\",\"password\":\"Valid123!\"}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        await Assert.ThrowsAsync<ArgumentException>(() => Auth(f).LoginAsync(legacy));
    }

    [Theory]
    [InlineData("password")]
    [InlineData("role-result")]
    public async Task RegistrationIdentityFailure_ClearsTrackingAndRollsBack(string stage)
    {
        await using var f = await AgentCreationTransactionTests.Fixture.CreateAsync();
        if (stage == "role-result")
        {
            f.Users.UserValidators.Clear();
            f.Users.UserValidators.Add(new RejectSecondValidation());
        }
        var result = await new AuthRepository(f.Db, f.Users).RegisterWithRoleAsync(
            new User { UserName = "failure", Email = "failure@example.test" }, stage == "password" ? "weak" : "Valid123!", "Agent");
        Assert.False(result.Succeeded);
        Assert.Empty(f.Db.ChangeTracker.Entries());
        await f.AssertCountsAsync(1, 0, 0, 0);
    }

    private sealed class RejectSecondValidation : IUserValidator<User>
    {
        private int _calls;
        public Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user) => Task.FromResult(
            ++_calls == 1 ? IdentityResult.Success : IdentityResult.Failed(new IdentityError { Code = "RejectedRole", Description = "Rejected role update" }));
    }
}
