using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DumpTether.App.Auth;
using DumpTether.App.Sync;
using DumpTether.App.Workspaces;
using DumpTether.Domain;
using Xunit;

namespace DumpTether.Api.Tests;

public sealed class SyncApiTests
{
    [Fact]
    public async Task ListWorkspaceRoots_WhenNotDesktop_ReturnsNotFound()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            enableDevelopmentLogin: true);
        using var client = factory.CreateClient();
        await LoginDevelopmentAsync(client);

        var response = await client.GetAsync("/api/sync/workspace-roots");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EnsureWorkspaceRoot_WhenDesktop_IsIdempotent()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop");
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;

        var first = await client.PostAsJsonAsync(
            "/api/sync/workspace-roots",
            new EnsureWorkspaceSyncRootRequest(workspaceId, "device-a"));
        var second = await client.PostAsJsonAsync(
            "/api/sync/workspace-roots",
            new EnsureWorkspaceSyncRootRequest(workspaceId, "device-a"));

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        var firstRoot = await first.Content.ReadFromJsonAsync<SyncRootResponse>();
        var secondRoot = await second.Content.ReadFromJsonAsync<SyncRootResponse>();
        var roots = await client.GetFromJsonAsync<List<SyncRootResponse>>(
            "/api/sync/workspace-roots");

        Assert.Equal(firstRoot!.Id, secondRoot!.Id);
        Assert.Equal(SyncRootStatus.LocalOnly, firstRoot.Status);
        Assert.Single(roots!);
    }

    [Fact]
    public async Task LinkWorkspaceRoot_PreventsDuplicateRemoteMapping()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop");
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var firstWorkspaceId = login.Workspaces.Single().Id;
        var secondWorkspace = await client.PostAsJsonAsync(
            "/api/workspaces",
            new { name = "Second local board" });
        secondWorkspace.EnsureSuccessStatusCode();
        var secondWorkspaceResponse =
            await secondWorkspace.Content.ReadFromJsonAsync<WorkspaceResponse>();
        var remoteWorkspaceId = Guid.NewGuid();
        var cloudUserId = Guid.NewGuid();

        var firstLink = await client.PostAsJsonAsync(
            "/api/sync/workspace-roots/link",
            new LinkWorkspaceSyncRootRequest(
                firstWorkspaceId,
                remoteWorkspaceId,
                cloudUserId,
                "device-a"));
        var duplicateLink = await client.PostAsJsonAsync(
            "/api/sync/workspace-roots/link",
            new LinkWorkspaceSyncRootRequest(
                secondWorkspaceResponse!.Id,
                remoteWorkspaceId,
                cloudUserId,
                "device-a"));

        firstLink.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, duplicateLink.StatusCode);
    }

    private static async Task<LoginUserResponse> LoginDesktopAsync(HttpClient client)
    {
        var loginResponse = await client.PostAsync("/api/auth/local-desktop", content: null);
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginUserResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.SessionToken);

        return login;
    }

    private static async Task<LoginUserResponse> LoginDevelopmentAsync(HttpClient client)
    {
        var loginResponse = await client.PostAsync("/api/auth/development-login", content: null);
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginUserResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.SessionToken);

        return login;
    }
}
