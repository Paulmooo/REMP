using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Remp.DataAccess.Data;
using Remp.Models.Entities;
using Remp.Repository.Repositories;
using Remp.Service.Services;
using Remp.Service.Validators;

namespace Remp.Tests;

// Relational tests: real Identity stores, FK constraints, join table and transactions.
// No application startup, SQL Server connection, seed hook or external service is used.
public class AgentCreationTransactionTests
{
    [Fact]
    public async Task Create_CommitsIdentityRoleSharedKeyProfileAndCompanyTogether()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.Service.CreateAgentAsync("company", UserServiceTests.ValidDto());
        fixture.Db.ChangeTracker.Clear();

        var user = await fixture.Db.Users.SingleAsync(x => x.Email == "agent@example.test");
        var profile = await fixture.Db.Agents.Include(x => x.User).Include(x => x.PhotographyCompanies).SingleAsync();
        Assert.Equal(user.Id, result.AgentId);
        Assert.Equal(user.Id, profile.Id);
        Assert.Equal(user.Id, profile.User.Id);
        Assert.Equal("company", Assert.Single(profile.PhotographyCompanies).Id);
        Assert.True(await fixture.Users.IsInRoleAsync(user, "Agent"));
        Assert.True(await fixture.Users.CheckPasswordAsync(user, result.TemporaryPassword));

        var duplicate = () => fixture.Service.CreateAgentAsync("company", UserServiceTests.ValidDto());
        await Assert.ThrowsAsync<Remp.Common.Exceptions.ConflictException>(duplicate);
        await fixture.AssertCountsAsync(2, 1, 1, 1);
    }

    [Fact]
    public async Task Create_WithoutOptionalAvatar_StillCommitsValidProfile()
    {
        await using var fixture = await Fixture.CreateAsync();
        var dto = UserServiceTests.ValidDto();
        dto.AvatarUrl = null!;
        var result = await fixture.Service.CreateAgentAsync("company", dto);
        fixture.Db.ChangeTracker.Clear();
        var profile = await fixture.Db.Agents.SingleAsync(x => x.Id == result.AgentId);
        Assert.Equal(string.Empty, profile.AvatarUrl);
        await fixture.AssertCountsAsync(2, 1, 1, 1);
    }

    [Theory]
    [InlineData("role")]
    [InlineData("profile")]
    [InlineData("company")]
    public async Task Create_WhenPersistenceFails_RollsBackAllAccountRowsAndCanRetry(string stage)
    {
        var failure = new FailOnceInterceptor();
        await using var fixture = await Fixture.CreateAsync(failure);
        if (stage == "company")
        {
            await fixture.Db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER fail_company_link BEFORE INSERT ON AgentPhotograpyCompany
                BEGIN SELECT RAISE(ABORT, 'injected company link failure'); END;
                """);
        }
        else failure.Stage = stage;

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Service.CreateAgentAsync("company", UserServiceTests.ValidDto()));

        // Observe the DB with a fresh context so tracking cannot hide failed persistence.
        await fixture.AssertCountsAsync(1, 0, 0, 0);
        if (stage == "company")
            await fixture.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_company_link");
        var result = await fixture.Service.CreateAgentAsync("company", UserServiceTests.ValidDto());
        Assert.False(string.IsNullOrWhiteSpace(result.AgentId));
        await fixture.AssertCountsAsync(2, 1, 1, 1);
    }

    [Fact]
    public async Task Create_WhenIdentityRejectsPassword_LeavesNoAccountOrRelations()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Users.Options.Password.RequiredLength = 100;
        var act = () => fixture.Service.CreateAgentAsync("company", UserServiceTests.ValidDto());
        await Assert.ThrowsAsync<ArgumentException>(act);
        await fixture.AssertCountsAsync(1, 0, 0, 0);
    }

    [Fact]
    public async Task Create_WhenRoleAssignmentReturnsFailure_RollsBackIdentityUser()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Users.UserValidators.Clear();
        fixture.Users.UserValidators.Add(new RejectRoleUpdateValidator());
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CreateAgentAsync("company", UserServiceTests.ValidDto()));
        await fixture.AssertCountsAsync(1, 0, 0, 0);
    }

    private sealed class RejectRoleUpdateValidator : IUserValidator<User>
    {
        private int _calls;
        public Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user) =>
            Task.FromResult(++_calls == 1 ? IdentityResult.Success :
                IdentityResult.Failed(new IdentityError { Code = "RejectedRoleUpdate", Description = "Role update rejected" }));
    }

    private sealed class FailOnceInterceptor : SaveChangesInterceptor
    {
        public string? Stage { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var entries = eventData.Context!.ChangeTracker.Entries().Where(x => x.State == EntityState.Added).ToList();
            var fail = Stage switch
            {
                "role" => entries.Any(x => x.Entity is IdentityUserRole<string>),
                "profile" => entries.Any(x => x.Entity is Agent),
                "company" => entries.Any(x => x.Metadata.GetTableName() == "AgentPhotograpyCompany"),
                _ => false
            };
            if (fail)
            {
                Stage = null;
                throw new DbUpdateException("Injected persistence failure");
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;
        public RempDbContext Db { get; }
        public UserManager<User> Users { get; }
        public UserService Service { get; }

        private Fixture(SqliteConnection connection, ServiceProvider provider, AsyncServiceScope scope)
        {
            _connection = connection;
            _provider = provider;
            _scope = scope;
            Db = scope.ServiceProvider.GetRequiredService<RempDbContext>();
            Users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            Service = new UserService(new UserRepository(Db, Users), new CreateAgentRequestValidator(),
                new UpdatePasswordRequestValidator(), Mock.Of<IMapper>());
        }

        public static async Task<Fixture> CreateAsync(SaveChangesInterceptor? interceptor = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<RempDbContext>(options =>
            {
                options.UseSqlite(connection);
                if (interceptor != null) options.AddInterceptors(interceptor);
            });
            services.AddIdentityCore<User>().AddRoles<Role>().AddEntityFrameworkStores<RempDbContext>();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateAsyncScope();
            var fixture = new Fixture(connection, provider, scope);
            await fixture.Db.Database.EnsureCreatedAsync();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            Assert.True((await roles.CreateAsync(new Role { Name = "Agent" })).Succeeded);
            var companyUser = new User { Id = "company", UserName = "company", Email = "company@example.test" };
            Assert.True((await fixture.Users.CreateAsync(companyUser)).Succeeded);
            fixture.Db.PhotographyCompanies.Add(new PhotographyCompany
            {
                Id = companyUser.Id, User = companyUser, PhotographyCompanyName = "Test Photography", Agents = []
            });
            await fixture.Db.SaveChangesAsync();
            fixture.Db.ChangeTracker.Clear();
            return fixture;
        }

        public async Task AssertCountsAsync(int users, int profiles, int roles, int links)
        {
            var options = new DbContextOptionsBuilder<RempDbContext>().UseSqlite(_connection).Options;
            await using var fresh = new RempDbContext(options);
            Assert.Equal(users, await fresh.Users.CountAsync());
            Assert.Equal(profiles, await fresh.Agents.CountAsync());
            Assert.Equal(roles, await fresh.UserRoles.CountAsync());
            var company = await fresh.PhotographyCompanies.Include(x => x.Agents).SingleAsync();
            Assert.Equal(links, company.Agents.Count);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
