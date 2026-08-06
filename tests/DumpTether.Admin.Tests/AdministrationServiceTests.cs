using DumpTether.App;
using DumpTether.App.Administration;
using DumpTether.Data;
using DumpTether.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DumpTether.Admin.Tests;

public sealed class AdministrationServiceTests
{
    [Fact]
    public async Task ListUsers_ReturnsOperationalMetadataWithoutSecrets()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        await environment.SeedUserAsync("alice@example.com", "Alice");

        var users = await environment.Service.ListUsersAsync(null, 100, CancellationToken.None);

        var user = Assert.Single(users);
        Assert.Equal("alice@example.com", user.Email);
        Assert.Equal("Alice", user.DisplayName);
        Assert.True(user.IsActive);
        Assert.Equal(1, user.OwnedBoardCount);
    }

    [Fact]
    public async Task LockUser_RevokesSessionsAndWritesAuditEvent()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var user = await environment.SeedUserAsync(
            "locked@example.com",
            "Locked User",
            addSession: true,
            addExpiredSession: true);

        var locked = await environment.Service.LockUserAsync(
            user.Email,
            "test-operator",
            "Account owner requested a temporary lock.",
            CancellationToken.None);

        Assert.True(locked);
        await using var verificationScope = environment.Services.CreateAsyncScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        var storedUser = await db.AppUsers.SingleAsync(candidate => candidate.Id == user.Id);
        var storedSessions = await db.UserSessions
            .Where(candidate => candidate.UserId == user.Id)
            .ToListAsync();
        var sessions = storedSessions
            .OrderBy(candidate => candidate.ExpiresAt)
            .ToList();
        var auditEvent = await db.OperatorAuditEvents.SingleAsync();
        Assert.False(storedUser.IsActive);
        Assert.Null(sessions[0].RevokedAt);
        Assert.NotNull(sessions[1].RevokedAt);
        Assert.Equal("user.lock", auditEvent.Action);
        Assert.Equal("test-operator", auditEvent.Actor);
    }

    [Fact]
    public async Task DeleteUser_RejectsMismatchedConfirmationEmail()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        await environment.SeedUserAsync("delete@example.com", "Delete User");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            environment.Service.DeleteUserAsync(
                "delete@example.com",
                "someone-else@example.com",
                "test-operator",
                "Test deletion.",
                CancellationToken.None));
    }

    [Fact]
    public async Task DeleteUser_PurgesOwnedBoardAndPreservesReferencedTemplate()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var target = await environment.SeedUserAsync("delete@example.com", "Delete User", addSession: true);
        var survivor = await environment.SeedUserAsync("survivor@example.com", "Survivor");

        Guid targetWorkspaceId;
        Guid survivorTaskId;
        Guid templateId;
        await using (var seedScope = environment.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
            targetWorkspaceId = await db.WorkspaceMemberships
                .Where(membership =>
                    membership.UserId == target.Id &&
                    membership.Role == WorkspaceMembershipRole.Owner)
                .Select(membership => membership.WorkspaceId)
                .SingleAsync();
            var survivorWorkspaceId = await db.WorkspaceMemberships
                .Where(membership =>
                    membership.UserId == survivor.Id &&
                    membership.Role == WorkspaceMembershipRole.Owner)
                .Select(membership => membership.WorkspaceId)
                .SingleAsync();
            var survivorProjectId = await db.Projects
                .Where(project => project.WorkspaceId == survivorWorkspaceId)
                .Select(project => project.Id)
                .SingleAsync();

            var template = TaskTemplate.Create(target.Id, "Shared shape", DateTimeOffset.UtcNow);
            var taskItem = TaskItem.Create(
                survivorWorkspaceId,
                survivorProjectId,
                "Keep this task",
                DateTimeOffset.UtcNow,
                template.Id);
            await db.TaskTemplates.AddAsync(template);
            await db.TaskItems.AddAsync(taskItem);
            await db.SaveChangesAsync();
            templateId = template.Id;
            survivorTaskId = taskItem.Id;
        }

        var result = await environment.Service.DeleteUserAsync(
            target.Email,
            target.Email,
            "test-operator",
            "Remove test account and its owned data.",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, result.DeletedBoardCount);
        Assert.Equal(1, result.PreservedTemplateCount);

        await using var verificationScope = environment.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        Assert.False(await verificationDb.AppUsers.AnyAsync(user => user.Id == target.Id));
        Assert.False(await verificationDb.Workspaces.AnyAsync(workspace => workspace.Id == targetWorkspaceId));
        Assert.True(await verificationDb.TaskItems.AnyAsync(taskItem => taskItem.Id == survivorTaskId));
        var preservedTemplate = await verificationDb.TaskTemplates.SingleAsync(template => template.Id == templateId);
        Assert.Null(preservedTemplate.OwnerUserId);
        Assert.Equal("user.delete", (await verificationDb.OperatorAuditEvents.SingleAsync()).Action);
    }

    private sealed class TestEnvironment : IAsyncDisposable
    {
        private readonly string _databasePath;

        private TestEnvironment(string databasePath, ServiceProvider services, IServiceScope serviceScope)
        {
            _databasePath = databasePath;
            Services = services;
            ServiceScope = serviceScope;
            Service = serviceScope.ServiceProvider.GetRequiredService<IAdministrationService>();
        }

        public ServiceProvider Services { get; }

        public IServiceScope ServiceScope { get; }

        public IAdministrationService Service { get; }

        public static async Task<TestEnvironment> CreateAsync()
        {
            var databasePath = Path.Combine(
                Path.GetTempPath(),
                $"dumptether-admin-tests-{Guid.NewGuid():N}.db");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "Sqlite",
                    ["Database:Sqlite:Path"] = databasePath
                })
                .Build();
            var services = new ServiceCollection()
                .AddDumpTetherApplication()
                .AddDumpTetherData(configuration)
                .BuildServiceProvider(validateScopes: true);

            await using (var migrationScope = services.CreateAsyncScope())
            {
                var db = migrationScope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
                await db.Database.EnsureCreatedAsync();
            }

            return new TestEnvironment(databasePath, services, services.CreateScope());
        }

        public async Task<AppUser> SeedUserAsync(
            string email,
            string displayName,
            bool addSession = false,
            bool addExpiredSession = false)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
            var now = DateTimeOffset.UtcNow;
            var user = AppUser.Create(email, displayName, "test-password-hash", now);
            var workspace = Workspace.Create($"{displayName} board", now);
            var project = Project.Create(workspace.Id, "General", now);

            await db.AppUsers.AddAsync(user);
            await db.Workspaces.AddAsync(workspace);
            await db.WorkspaceMemberships.AddAsync(
                WorkspaceMembership.Create(
                    workspace.Id,
                    user.Id,
                    WorkspaceMembershipRole.Owner,
                    now));
            await db.Projects.AddAsync(project);

            if (addSession)
            {
                await db.UserSessions.AddAsync(
                    UserSession.Create(
                        user.Id,
                        $"session-hash-{Guid.NewGuid():N}",
                        now,
                        now.AddDays(30)));
            }

            if (addExpiredSession)
            {
                await db.UserSessions.AddAsync(
                    UserSession.Create(
                        user.Id,
                        $"expired-session-hash-{Guid.NewGuid():N}",
                        now.AddDays(-2),
                        now.AddDays(-1)));
            }

            await db.SaveChangesAsync();
            return user;
        }

        public async ValueTask DisposeAsync()
        {
            ServiceScope.Dispose();
            await Services.DisposeAsync();
            SqliteConnection.ClearAllPools();

            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
    }
}
