using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DumpTether.App.Auth;
using DumpTether.App.Tasks;
using DumpTether.Data;
using Microsoft.EntityFrameworkCore;
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
        Assert.Contains(login.Workspaces, workspace => workspace.Name == "All Tasks");
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

    private static async Task<RegisterUserResponse> RegisterAsync(
        HttpClient client,
        string email,
        string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email,
                password,
                displayName = email.Split('@')[0]
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
}
