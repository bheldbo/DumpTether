using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DumpTether.App.Auth;
using DumpTether.App.Sync;
using DumpTether.App.Tasks;
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

    [Fact]
    public async Task CreateTask_WhenDesktop_ReturnsLocalOnlySyncState()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop");
        using var client = factory.CreateClient();
        await LoginDesktopAsync(client);

        var created = await CreateTaskItemAsync(client, "Local task");
        var tasks = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks");
        var detail = await client.GetFromJsonAsync<TaskItemDetailResponse>(
            $"/api/tasks/{created.Id}");

        Assert.Equal(SyncMappingStatus.LocalOnly.ToString(), created.SyncState?.Status);
        Assert.Equal(SyncMappingStatus.LocalOnly.ToString(), tasks!.Single().SyncState?.Status);
        Assert.Equal(SyncMappingStatus.LocalOnly.ToString(), detail!.SyncState?.Status);
    }

    [Fact]
    public async Task MarkTaskItemSynced_WhenDesktop_StoresRemoteLink()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop");
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;
        var created = await CreateTaskItemAsync(client, "Sync me");
        var remoteTaskId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/tasks/{created.Id}/synced",
            new MarkTaskItemSyncedRequest(remoteTaskId, "v1"));

        response.EnsureSuccessStatusCode();
        var syncState = await response.Content.ReadFromJsonAsync<TaskSyncStateResponse>();

        Assert.Equal(SyncMappingStatus.Synced.ToString(), syncState!.Status);
        Assert.Equal(remoteTaskId, syncState.RemoteId);
        Assert.Equal("v1", syncState.LastRemoteVersion);
        Assert.NotNull(syncState.LastSyncedAt);
        Assert.Null(syncState.LastError);
    }

    [Fact]
    public async Task MarkTaskItemSynced_WhenTaskIsMissing_ReturnsBadRequest()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop");
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/tasks/{Guid.NewGuid()}/synced",
            new MarkTaskItemSyncedRequest(Guid.NewGuid(), "v1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MarkTaskItemSyncFailed_WhenDesktop_ReturnsFailureState()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop");
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;
        var created = await CreateTaskItemAsync(client, "Fail gracefully");

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/tasks/{created.Id}/failed",
            new MarkTaskItemSyncFailedRequest("Cloud sync timed out."));

        response.EnsureSuccessStatusCode();
        var syncState = await response.Content.ReadFromJsonAsync<TaskSyncStateResponse>();

        Assert.Equal(SyncMappingStatus.SyncFailed.ToString(), syncState!.Status);
        Assert.Equal("Cloud sync timed out.", syncState.LastError);
        Assert.NotNull(syncState.LastAttemptedAt);
        Assert.Null(syncState.LastSyncedAt);
    }

    [Fact]
    public async Task MarkTaskItemSynced_WhenNotDesktop_ReturnsNotFound()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            enableDevelopmentLogin: true);
        using var client = factory.CreateClient();
        await LoginDevelopmentAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{Guid.NewGuid()}/tasks/{Guid.NewGuid()}/synced",
            new MarkTaskItemSyncedRequest(Guid.NewGuid(), "v1"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    private static async Task<TaskItemDetailResponse> CreateTaskItemAsync(
        HttpClient client,
        string title)
    {
        var response = await client.PostAsJsonAsync("/api/tasks", new { title });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>())!;
    }
}
