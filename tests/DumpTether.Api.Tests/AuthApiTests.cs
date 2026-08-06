using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.ComponentModel.DataAnnotations;
using DumpTether.Api;
using DumpTether.App.Auth;
using DumpTether.App.Tasks;
using DumpTether.Data;
using DumpTether.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DumpTether.Api.Tests;

public sealed class AuthApiTests
{
    [Fact]
    public async Task PostRegister_CreatesUserAndWorkspaceMembership()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var registered = await RegisterAsync(client, "user@example.com", "correct horse battery");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        var user = await dbContext.AppUsers.SingleAsync(user => user.Id == registered.User.Id);
        var membership = await dbContext.WorkspaceMemberships.SingleAsync(
            candidate =>
                candidate.UserId == registered.User.Id &&
                candidate.WorkspaceId == registered.Workspace.Id);

        Assert.Equal("user@example.com", user.Email);
        Assert.Equal("USER@EXAMPLE.COM", user.NormalizedEmail);
        Assert.True(user.IsActive);
        Assert.Equal("Owner", membership.Role.ToString());
    }

    [Fact]
    public async Task PostRegister_RejectsDuplicateEmail()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        await RegisterAsync(client, "duplicate@example.com", "correct horse battery");

        var duplicate = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = "DUPLICATE@example.com",
                password = "correct horse battery"
            });

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task PostRegister_WhenLegalAcceptanceIsRequired_RejectsMissingAcceptance()
    {
        using var factory = new DumpTetherApiFactory(
            extraConfiguration: RequiredLegalConfiguration());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = "missing-legal@example.com",
                password = "correct horse battery"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        Assert.Empty(await dbContext.AppUsers.ToListAsync());
        Assert.Empty(await dbContext.LegalAcceptances.ToListAsync());
    }

    [Fact]
    public async Task PostRegister_WhenLegalAcceptanceIsRequired_RecordsCurrentVersions()
    {
        using var factory = new DumpTetherApiFactory(
            extraConfiguration: RequiredLegalConfiguration());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = "accepted-legal@example.com",
                password = "correct horse battery",
                legalAcceptance = new
                {
                    termsAccepted = true,
                    termsVersion = "terms-2026-08",
                    privacyNoticeAcknowledged = true,
                    privacyNoticeVersion = "privacy-2026-08"
                }
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        var acceptances = await dbContext.LegalAcceptances
            .OrderBy(acceptance => acceptance.DocumentType)
            .ToListAsync();

        Assert.Collection(
            acceptances,
            acceptance =>
            {
                Assert.Equal(LegalDocumentType.TermsOfUse, acceptance.DocumentType);
                Assert.Equal("terms-2026-08", acceptance.DocumentVersion);
            },
            acceptance =>
            {
                Assert.Equal(LegalDocumentType.PrivacyNotice, acceptance.DocumentType);
                Assert.Equal("privacy-2026-08", acceptance.DocumentVersion);
            });
    }

    [Fact]
    public async Task PostRegister_WhenLegalVersionIsStale_RejectsWithoutCreatingUser()
    {
        using var factory = new DumpTetherApiFactory(
            extraConfiguration: RequiredLegalConfiguration());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = "stale-legal@example.com",
                password = "correct horse battery",
                legalAcceptance = new
                {
                    termsAccepted = true,
                    termsVersion = "old-terms",
                    privacyNoticeAcknowledged = true,
                    privacyNoticeVersion = "privacy-2026-08"
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        Assert.Empty(await dbContext.AppUsers.ToListAsync());
    }

    [Fact]
    public async Task PostRegister_WhenSignupClosed_Rejects()
    {
        using var factory = new DumpTetherApiFactory(
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Auth:SignupMode"] = "Closed"
            });
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = "closed@example.com",
                password = "correct horse battery"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        Assert.Empty(await dbContext.AppUsers.ToListAsync());
    }

    [Fact]
    public async Task PostRegister_WhenWhitelistMode_AllowsConfiguredEmailAndDomain()
    {
        using var factory = new DumpTetherApiFactory(
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Auth:SignupMode"] = "Whitelist",
                ["Auth:SignupWhitelistEmails:0"] = "friend@example.com",
                ["Auth:SignupWhitelistDomains:0"] = "heldbo.net"
            });
        using var client = factory.CreateClient();

        var configuredEmail = await RegisterAsync(
            client,
            "friend@example.com",
            "correct horse battery");
        var configuredDomain = await RegisterAsync(
            client,
            "bjarke@heldbo.net",
            "correct horse battery");

        Assert.Equal("friend@example.com", configuredEmail.User.Email);
        Assert.Equal("bjarke@heldbo.net", configuredDomain.User.Email);
    }

    [Fact]
    public async Task PostRegister_WhenWhitelistMode_RejectsOtherEmail()
    {
        using var factory = new DumpTetherApiFactory(
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Auth:SignupMode"] = "Whitelist",
                ["Auth:SignupWhitelistDomains:0"] = "heldbo.net"
            });
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = "stranger@example.com",
                password = "correct horse battery"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostRegister_WhenInviteOnly_RequiresValidInviteCode()
    {
        using var factory = new DumpTetherApiFactory(
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Auth:SignupMode"] = "InviteOnly",
                ["Auth:SignupInviteCodes:0"] = "alpha-invite"
            });
        using var client = factory.CreateClient();

        var rejected = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = "no-code@example.com",
                password = "correct horse battery"
            });
        var accepted = await RegisterAsync(
            client,
            "has-code@example.com",
            "correct horse battery",
            "alpha-invite");

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("has-code@example.com", accepted.User.Email);
    }

    [Fact]
    public async Task PostRegister_DoesNotStoreRawPassword()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        const string password = "correct horse battery";

        var registered = await RegisterAsync(client, "hash@example.com", password);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        var user = await dbContext.AppUsers.SingleAsync(user => user.Id == registered.User.Id);

        Assert.NotEqual(password, user.PasswordHash);
        Assert.DoesNotContain(password, user.PasswordHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostLogin_WithValidCredentials_CreatesSession()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "login@example.com", "correct horse battery");

        var login = await LoginAsync(client, "login@example.com", "correct horse battery");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        var session = await dbContext.UserSessions.SingleAsync();

        Assert.NotEqual(Guid.Empty, login.User.Id);
        Assert.False(string.IsNullOrWhiteSpace(login.SessionToken));
        Assert.NotEqual(login.SessionToken, session.SessionTokenHash);
        Assert.Null(session.RevokedAt);
        Assert.Equal(UserSessionType.Browser, login.Session.SessionType);
        Assert.Equal(UserSessionType.Browser, session.SessionType);
    }

    [Fact]
    public async Task PostDesktopCloudLogin_CreatesDesktopCloudSession()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "desktop-cloud@example.com", "correct horse battery");

        var response = await client.PostAsJsonAsync(
            "/api/auth/desktop-cloud-login",
            new
            {
                email = "desktop-cloud@example.com",
                password = "correct horse battery",
                deviceName = "DumpTether desktop"
            });

        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginUserResponse>();
        Assert.Equal(UserSessionType.DesktopCloud, login!.Session.SessionType);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        var session = await dbContext.UserSessions.SingleAsync();
        Assert.Equal(UserSessionType.DesktopCloud, session.SessionType);
    }

    [Fact]
    public async Task PostLogin_UsesConfiguredSessionDays()
    {
        using var factory = new DumpTetherApiFactory(
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Auth:SessionDays"] = "2"
            });
        using var client = factory.CreateClient();
        await RegisterAsync(client, "session-days@example.com", "correct horse battery");

        var beforeExpectedExpiry = DateTimeOffset.UtcNow.AddDays(2).AddMinutes(-2);
        var login = await LoginAsync(client, "session-days@example.com", "correct horse battery");
        var afterExpectedExpiry = DateTimeOffset.UtcNow.AddDays(2).AddMinutes(2);

        Assert.InRange(login.ExpiresAt, beforeExpectedExpiry, afterExpectedExpiry);
    }

    [Fact]
    public async Task PostLogin_CleansOldInactiveSessions()
    {
        using var factory = new DumpTetherApiFactory(
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Auth:SessionCleanupDays"] = "90"
            });
        using var client = factory.CreateClient();
        var registered = await RegisterAsync(
            client,
            "session-cleanup@example.com",
            "correct horse battery");
        var now = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
            var oldExpiredSession = UserSession.Create(
                registered.User.Id,
                "old-expired-session-hash",
                now.AddDays(-130),
                now.AddDays(-120));
            var recentExpiredSession = UserSession.Create(
                registered.User.Id,
                "recent-expired-session-hash",
                now.AddDays(-2),
                now.AddDays(-1));
            var oldRevokedSession = UserSession.Create(
                registered.User.Id,
                "old-revoked-session-hash",
                now.AddDays(-130),
                now.AddDays(30));
            oldRevokedSession.Revoke(now.AddDays(-120));
            dbContext.UserSessions.AddRange(
                oldExpiredSession,
                recentExpiredSession,
                oldRevokedSession);
            await dbContext.SaveChangesAsync();
        }

        await LoginAsync(client, "session-cleanup@example.com", "correct horse battery");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
            var sessionHashes = await dbContext.UserSessions
                .Select(session => session.SessionTokenHash)
                .ToListAsync();

            Assert.DoesNotContain("old-expired-session-hash", sessionHashes);
            Assert.DoesNotContain("old-revoked-session-hash", sessionHashes);
            Assert.Contains("recent-expired-session-hash", sessionHashes);
            Assert.Equal(2, sessionHashes.Count);
        }
    }

    [Fact]
    public async Task PostLogin_WithInvalidCredentials_Fails()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "bad-login@example.com", "correct horse battery");

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "bad-login@example.com",
                password = "wrong password"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostLogout_RevokesCurrentSession()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "logout@example.com", "correct horse battery");
        var login = await LoginAsync(client, "logout@example.com", "correct horse battery");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.SessionToken);

        var response = await client.PostAsync("/api/auth/logout", content: null);

        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        var session = await dbContext.UserSessions.SingleAsync();

        Assert.NotNull(session.RevokedAt);
    }

    [Fact]
    public async Task GetSessions_ReturnsCurrentUserSessions()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "sessions@example.com", "correct horse battery");
        var login = await LoginAsync(client, "sessions@example.com", "correct horse battery");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.SessionToken);

        var response = await client.GetAsync("/api/auth/sessions");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {response.StatusCode}. Body: {body}");

        var sessions = await response.Content.ReadFromJsonAsync<List<AuthSessionListItemResponse>>();

        Assert.NotNull(sessions);
        var session = Assert.Single(sessions!, candidate => candidate.Id == login.Session.Id);
        Assert.Equal(login.Session.Id, session.Id);
        Assert.True(session.IsCurrent);
        Assert.Equal(UserSessionType.Browser, session.SessionType);
        Assert.Equal("test client", session.DeviceName);
        Assert.Null(session.RevokedAt);
    }

    [Fact]
    public async Task DeleteSession_RevokesOwnOtherSession()
    {
        using var factory = new DumpTetherApiFactory();
        using var registrationClient = factory.CreateClient();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        await RegisterAsync(registrationClient, "revoke-session@example.com", "correct horse battery");
        var firstLogin = await LoginAsync(firstClient, "revoke-session@example.com", "correct horse battery");
        var secondLogin = await LoginAsync(secondClient, "revoke-session@example.com", "correct horse battery");
        firstClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", firstLogin.SessionToken);

        var response = await firstClient.DeleteAsync($"/api/auth/sessions/{secondLogin.Session.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        var secondSession = await dbContext.UserSessions.SingleAsync(
            session => session.Id == secondLogin.Session.Id);
        var firstSession = await dbContext.UserSessions.SingleAsync(
            session => session.Id == firstLogin.Session.Id);

        Assert.NotNull(secondSession.RevokedAt);
        Assert.Null(firstSession.RevokedAt);
    }

    [Fact]
    public async Task PostLogin_InactiveUser_Fails()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var registered = await RegisterAsync(client, "inactive@example.com", "correct horse battery");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
            var user = await dbContext.AppUsers.SingleAsync(user => user.Id == registered.User.Id);
            user.Deactivate(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "inactive@example.com",
                password = "correct horse battery"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedTaskQuery_WhenAuthRequired_ReturnsUnauthorized()
    {
        using var factory = new DumpTetherApiFactory(requireAuthentication: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedTemplateQuery_WhenAuthRequired_ReturnsUnauthorized()
    {
        using var factory = new DumpTetherApiFactory(requireAuthentication: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/templates");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostDevelopmentLogin_WhenEnabled_CreatesNormalSession()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            enableDevelopmentLogin: true);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/auth/development-login", content: null);

        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginUserResponse>();
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login!.SessionToken));
        Assert.Equal(UserSessionType.Development, login.Session.SessionType);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.SessionToken);
        var currentUser = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        Assert.Equal("dev@dumptether.local", currentUser!.User.Email);
        Assert.Contains(currentUser.Workspaces, workspace => workspace.Name == "All Tasks");
    }

    [Fact]
    public async Task PostGuestLogin_WhenEnabled_CreatesTemporaryUserSession()
    {
        using var factory = new DumpTetherApiFactory(requireAuthentication: true);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/auth/guest", content: null);

        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginUserResponse>();

        Assert.NotNull(login);
        Assert.EndsWith("@guest.dumptether.local", login!.User.Email);
        Assert.False(string.IsNullOrWhiteSpace(login.SessionToken));
        Assert.Equal(UserSessionType.Guest, login.Session.SessionType);
        Assert.Contains(login.Workspaces, workspace => workspace.Name == "All Tasks");
    }

    [Fact]
    public async Task GuestSession_CannotCreatePersistedTasks()
    {
        using var factory = new DumpTetherApiFactory(requireAuthentication: true);
        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsync("/api/auth/guest", content: null);
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginUserResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.SessionToken);

        var response = await client.PostAsJsonAsync(
            "/api/tasks",
            new { title = "Should not persist" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        Assert.Empty(await dbContext.TaskItems.ToListAsync());
    }

    [Fact]
    public async Task GetOptions_ReturnsSignupMode()
    {
        using var factory = new DumpTetherApiFactory(
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Auth:SignupMode"] = "InviteOnly",
                ["Auth:SignupInviteCodes:0"] = "alpha-invite"
            });
        using var client = factory.CreateClient();

        var options = await client.GetFromJsonAsync<AuthClientOptionsResponse>("/api/auth/options");

        Assert.NotNull(options);
        Assert.Equal(AuthSignupMode.InviteOnly, options!.SignupMode);
    }

    [Fact]
    public async Task PostLocalDesktopLogin_WhenNotDesktop_ReturnsNotFound()
    {
        using var factory = new DumpTetherApiFactory(requireAuthentication: true);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/auth/local-desktop", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostLocalDesktopLogin_WhenEnabledForSqliteDevelopment_CreatesPersistentLocalUserSession()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Auth:EnableLocalDesktopLogin"] = "true"
            });
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/auth/local-desktop", content: null);

        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginUserResponse>();

        Assert.NotNull(login);
        Assert.Equal(UserSessionType.DesktopLocal, login!.Session.SessionType);
        Assert.Contains(login.Workspaces, workspace => workspace.Name == "All Tasks");
    }

    [Fact]
    public async Task PostLocalDesktopLogin_WhenEnabledForPostgres_ReturnsNotFound()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Postgres",
                ["Auth:EnableLocalDesktopLogin"] = "true"
            });
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/auth/local-desktop", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostLocalDesktopLogin_WhenDesktop_CreatesPersistentLocalUserSession()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop");
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/auth/local-desktop", content: null);

        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginUserResponse>();

        Assert.NotNull(login);
        Assert.Equal("local@desktop.dumptether.local", login!.User.Email);
        Assert.Equal("Local user", login.User.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(login.SessionToken));
        Assert.Equal(UserSessionType.DesktopLocal, login.Session.SessionType);
        Assert.Contains(login.Workspaces, workspace => workspace.Name == "All Tasks");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.SessionToken);
        var currentUser = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        Assert.Equal(login.User.Id, currentUser!.User.Id);
    }

    [Fact]
    public async Task PostLocalDesktopLogin_WhenRepeated_ReusesLocalUserAndWorkspace()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop");
        using var client = factory.CreateClient();

        var first = await client.PostAsync("/api/auth/local-desktop", content: null);
        var second = await client.PostAsync("/api/auth/local-desktop", content: null);

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        var firstLogin = await first.Content.ReadFromJsonAsync<LoginUserResponse>();
        var secondLogin = await second.Content.ReadFromJsonAsync<LoginUserResponse>();

        Assert.Equal(firstLogin!.User.Id, secondLogin!.User.Id);
        Assert.Equal(firstLogin.Workspaces.Single().Id, secondLogin.Workspaces.Single().Id);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        Assert.Equal(1, await dbContext.AppUsers.CountAsync());
        Assert.Equal(1, await dbContext.Workspaces.CountAsync());
        Assert.Equal(2, await dbContext.UserSessions.CountAsync());
    }

    [Fact]
    public async Task PostLogout_ForDesktopLocalSession_DoesNotRevokeLocalIdentity()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop");
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsync("/api/auth/local-desktop", content: null);
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginUserResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.SessionToken);

        var logoutResponse = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, logoutResponse.StatusCode);
        var current = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.Equal(login.User.Id, current!.User.Id);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        var session = await dbContext.UserSessions.SingleAsync(
            candidate => candidate.Id == login.Session.Id);
        Assert.Null(session.RevokedAt);
    }

    [Fact]
    public async Task DeleteSession_ForDesktopLocalSession_DoesNotRevokeLocalIdentity()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop");
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsync("/api/auth/local-desktop", content: null);
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginUserResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.SessionToken);

        var revokeResponse = await client.DeleteAsync($"/api/auth/sessions/{login.Session.Id}");

        Assert.Equal(HttpStatusCode.NotFound, revokeResponse.StatusCode);
        var current = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.Equal(login.User.Id, current!.User.Id);
    }

    [Fact]
    public async Task LocalDesktopSession_CanCreateAndReadTaskWithoutCloudLogin()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop");
        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsync("/api/auth/local-desktop", content: null);
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginUserResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.SessionToken);

        var createResponse = await client.PostAsJsonAsync(
            "/api/tasks",
            new { title = "Offline local task" });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<TaskItemDetailResponse>();
        var tasks = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks");

        Assert.NotNull(created);
        Assert.Contains(tasks!, task => task.Id == created!.Id && task.Title == "Offline local task");
    }

    [Fact]
    public async Task UnsafeCookieAuthenticatedRequest_WithoutCsrfHeader_IsRejected()
    {
        using var factory = new DumpTetherApiFactory(requireAuthentication: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        await RegisterAsync(client, "cookie-no-csrf@example.com", "correct horse battery");
        var loginResponse = await LoginWithResponseAsync(
            client,
            "cookie-no-csrf@example.com",
            "correct horse battery");
        var sessionCookie = GetSetCookie(loginResponse, "DumpTether.Session");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tasks")
        {
            Content = JsonContent.Create(new { title = "Cookie-only task" })
        };
        request.Headers.Add("Cookie", sessionCookie);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnsafeCookieAuthenticatedRequest_WithQueryTokenOutsideLive_IsRejected()
    {
        using var factory = new DumpTetherApiFactory(requireAuthentication: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        await RegisterAsync(client, "cookie-query-token@example.com", "correct horse battery");
        var loginResponse = await LoginWithResponseAsync(
            client,
            "cookie-query-token@example.com",
            "correct horse battery");
        var sessionCookie = GetSetCookie(loginResponse, "DumpTether.Session");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/tasks?access_token=not-a-live-token")
        {
            Content = JsonContent.Create(new { title = "Cookie query token task" })
        };
        request.Headers.Add("Cookie", sessionCookie);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnsafeCookieAuthenticatedRequest_WithCsrfHeader_Succeeds()
    {
        using var factory = new DumpTetherApiFactory(requireAuthentication: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        await RegisterAsync(client, "cookie-csrf@example.com", "correct horse battery");
        var loginResponse = await LoginWithResponseAsync(
            client,
            "cookie-csrf@example.com",
            "correct horse battery");
        var sessionCookie = GetSetCookie(loginResponse, "DumpTether.Session");
        var csrfCookie = GetSetCookie(loginResponse, "DumpTether.Csrf");
        var csrfToken = GetCookieValue(csrfCookie, "DumpTether.Csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tasks")
        {
            Content = JsonContent.Create(new { title = "Cookie CSRF task" })
        };
        request.Headers.Add("Cookie", $"{sessionCookie}; {csrfCookie}");
        request.Headers.Add("X-DumpTether-CSRF", csrfToken);

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task SecurityHeaders_AreReturnedOnHealthCheck()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        Assert.Equal(
            "nosniff",
            response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(
            "DENY",
            response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal(
            "strict-origin-when-cross-origin",
            response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task ReadinessHealthCheck_WhenDatabaseIsAvailable_ReturnsHealthy()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("\"status\":\"healthy\"", body);
    }

    [Fact]
    public async Task HealthChecks_WhenRequestRateIsExcessive_AreRateLimited()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        HttpResponseMessage? response = null;

        for (var index = 0; index < 61; index++)
        {
            response?.Dispose();
            response = await client.GetAsync("/health/live");
        }

        using (response)
        {
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [Fact]
    public void Startup_WhenEmailConfirmationEnabledWithoutProvider_ThrowsHelpfulError()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailConfirmation:Enabled"] = "true"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => RuntimeConfigurationValidator.Validate(configuration, isDevelopment: true));

        Assert.Contains("DumpTether configuration is incomplete", exception.Message);
        Assert.Contains("Email:Provider", exception.Message);
    }

    [Fact]
    public void Startup_WhenBooleanContainsInlineComment_ThrowsHelpfulError()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Provider"] = "Smtp",
                ["Email:Smtp:UseAuthentication"] = "true # Credentials are required"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => RuntimeConfigurationValidator.Validate(configuration, isDevelopment: true));

        Assert.Contains("Email:Smtp:UseAuthentication", exception.Message);
        Assert.Contains("Do not append inline comments", exception.Message);
    }

    [Fact]
    public void Startup_WhenOAuthEnabledWithoutProviderConfig_ThrowsHelpfulError()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OAuth:Microsoft:Enabled"] = "true"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => RuntimeConfigurationValidator.Validate(configuration, isDevelopment: true));

        Assert.Contains("DumpTether configuration is incomplete", exception.Message);
        Assert.Contains("OAuth:Microsoft:ClientId", exception.Message);
        Assert.Contains("OAuth:Microsoft:ClientSecret", exception.Message);
        Assert.Contains("OAuth:Microsoft:TenantId", exception.Message);
    }

    [Fact]
    public void Startup_WhenMailpitStyleSmtpIsConfigured_DoesNotRequireCredentials()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Provider"] = "Smtp",
                ["Email:FromEmail"] = "noreply@dumptether.local",
                ["Email:Smtp:Host"] = "localhost",
                ["Email:Smtp:Port"] = "1025",
                ["Email:Smtp:UseAuthentication"] = "false",
                ["Email:Smtp:EnableSsl"] = "false"
            })
            .Build();

        RuntimeConfigurationValidator.Validate(configuration, isDevelopment: true);
    }

    [Fact]
    public void Startup_WhenDesktopRuntimeIsLoopbackSqlite_PassesValidation()
    {
        var configuration = BuildDesktopConfiguration();

        RuntimeConfigurationValidator.Validate(
            configuration,
            isDevelopment: false,
            isDesktop: true);
    }

    [Theory]
    [InlineData("Urls", "http://0.0.0.0:55869")]
    [InlineData("Database:Provider", "Postgres")]
    [InlineData("Auth:RequireAuthentication", "false")]
    [InlineData("Auth:AllowGuestSessions", "true")]
    [InlineData("Auth:EnableLocalDesktopLogin", "false")]
    [InlineData("Cors:AllowedOrigins:0", "http://example.test")]
    [InlineData("Desktop:BootstrapToken", "not-a-valid-token")]
    public void Startup_WhenDesktopRuntimeBoundaryIsWeakened_Throws(
        string key,
        string value)
    {
        var values = BuildDesktopConfigurationValues();
        values[key] = value;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        Assert.Throws<InvalidOperationException>(
            () => RuntimeConfigurationValidator.Validate(
                configuration,
                isDevelopment: false,
                isDesktop: true));
    }

    [Fact]
    public async Task DesktopBootstrapToken_WhenConfigured_IsRequiredForApiRequests()
    {
        var token = new string('a', 64);
        using var factory = new DumpTetherApiFactory(
            environmentName: "Desktop",
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Desktop:BootstrapToken"] = token
            });
        using var client = factory.CreateClient();

        var rejected = await client.GetAsync("/api/auth/options");
        client.DefaultRequestHeaders.Add("X-DumpTether-Desktop-Bootstrap", token);
        var accepted = await client.GetAsync("/api/auth/options");

        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Theory]
    [InlineData("google")]
    [InlineData("facebook")]
    public async Task GetOAuth_ForUnsupportedProvider_ReturnsNotFound(string provider)
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/auth/oauth/{provider}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExternalLogin_DoesNotSilentlyLinkExistingPasswordAccountByEmail()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "existing@example.com", "correct horse battery");
        using var scope = factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => authService.ExternalLoginAsync(
                new ExternalLoginRequest(
                    "microsoft",
                    "tenant-id:object-id",
                    "existing@example.com",
                    "Existing user"),
                new AuthRequestMetadata("test", "127.0.0.1"),
                CancellationToken.None));

        Assert.Contains("cannot be connected automatically", exception.Message);
    }

    [Fact]
    public async Task ExternalLogin_WhenCreatingUser_RecordsRequiredLegalAcceptance()
    {
        using var factory = new DumpTetherApiFactory(
            extraConfiguration: RequiredLegalConfiguration());
        using var scope = factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        await authService.ExternalLoginAsync(
            new ExternalLoginRequest(
                "microsoft",
                "tenant-id:new-object-id",
                "external-legal@example.com",
                "External legal user",
                new LegalAcceptanceSubmission(
                    true,
                    "terms-2026-08",
                    true,
                    "privacy-2026-08")),
            new AuthRequestMetadata("test", "127.0.0.1"),
            CancellationToken.None);

        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        var acceptances = await dbContext.LegalAcceptances
            .OrderBy(acceptance => acceptance.DocumentType)
            .ToListAsync();

        Assert.Collection(
            acceptances,
            acceptance => Assert.Equal(
                LegalDocumentType.TermsOfUse,
                acceptance.DocumentType),
            acceptance => Assert.Equal(
                LegalDocumentType.PrivacyNotice,
                acceptance.DocumentType));
    }

    [Fact]
    public void Startup_WhenInviteOnlySignupHasNoInviteCodes_ThrowsHelpfulError()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:SignupMode"] = "InviteOnly"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => RuntimeConfigurationValidator.Validate(configuration, isDevelopment: true));

        Assert.Contains("DumpTether configuration is incomplete", exception.Message);
        Assert.Contains("Auth:SignupInviteCodes", exception.Message);
    }

    [Fact]
    public async Task AuthenticatedTaskQueries_AreScopedToUserWorkspaceMembership()
    {
        using var factory = new DumpTetherApiFactory();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        await RegisterAsync(firstClient, "first@example.com", "correct horse battery");
        await RegisterAsync(secondClient, "second@example.com", "correct horse battery");
        var firstLogin = await LoginAsync(firstClient, "first@example.com", "correct horse battery");
        var secondLogin = await LoginAsync(secondClient, "second@example.com", "correct horse battery");
        firstClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", firstLogin.SessionToken);
        secondClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", secondLogin.SessionToken);

        var created = await firstClient.PostAsJsonAsync(
            "/api/tasks",
            new { title = "Private first user task" });
        created.EnsureSuccessStatusCode();
        var task = await created.Content.ReadFromJsonAsync<TaskItemDetailResponse>();
        var secondUserTasks = await secondClient.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks");

        Assert.NotNull(task);
        Assert.DoesNotContain(secondUserTasks!, candidate => candidate.Id == task.Id);
    }

    private static IConfiguration BuildDesktopConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(BuildDesktopConfigurationValues())
            .Build();

    private static Dictionary<string, string?> RequiredLegalConfiguration() =>
        new()
        {
            ["Legal:RequireAcceptance"] = "true",
            ["Legal:TermsVersion"] = "terms-2026-08",
            ["Legal:PrivacyNoticeVersion"] = "privacy-2026-08",
            ["Legal:OperatorName"] = "DumpTether test operator",
            ["Legal:PrivacyContactEmail"] = "privacy@example.com"
        };

    private static Dictionary<string, string?> BuildDesktopConfigurationValues() =>
        new()
        {
            ["Urls"] = "http://127.0.0.1:55869",
            ["Database:Provider"] = "Sqlite",
            ["Database:ApplyMigrationsOnStartup"] = "true",
            ["Auth:RequireAuthentication"] = "true",
            ["Auth:AllowGuestSessions"] = "false",
            ["Auth:SignupMode"] = "Closed",
            ["Auth:EnableDevelopmentLogin"] = "false",
            ["Auth:EnableLocalDesktopLogin"] = "true",
            ["EmailConfirmation:Enabled"] = "false",
            ["Email:Provider"] = "None",
            ["Mfa:Email:Enabled"] = "false",
            ["OAuth:Microsoft:Enabled"] = "false",
            ["Cors:AllowedOrigins:0"] = "http://tauri.localhost"
        };

    private static async Task<RegisterUserResponse> RegisterAsync(
        HttpClient client,
        string email,
        string password,
        string? inviteCode = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email,
                password,
                displayName = email.Split('@')[0],
                inviteCode
            });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created, got {response.StatusCode}. Body: {body}");

        return (await response.Content.ReadFromJsonAsync<RegisterUserResponse>())!;
    }

    private static async Task<LoginUserResponse> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email,
                password,
                deviceName = "test client"
            });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {response.StatusCode}. Body: {body}");

        return (await response.Content.ReadFromJsonAsync<LoginUserResponse>())!;
    }

    private static Task<HttpResponseMessage> LoginWithResponseAsync(
        HttpClient client,
        string email,
        string password) =>
        client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email,
                password,
                deviceName = "test client"
            });

    private static string GetSetCookie(HttpResponseMessage response, string cookieName)
    {
        var setCookieValues = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values
            : Array.Empty<string>();
        var setCookie = setCookieValues.Single(
            cookie => cookie.StartsWith($"{cookieName}=", StringComparison.Ordinal));

        return setCookie.Split(';', 2)[0];
    }

    private static string GetCookieValue(string cookie, string cookieName)
    {
        var prefix = $"{cookieName}=";
        Assert.StartsWith(prefix, cookie, StringComparison.Ordinal);
        return WebUtility.UrlDecode(cookie[prefix.Length..]);
    }
}
