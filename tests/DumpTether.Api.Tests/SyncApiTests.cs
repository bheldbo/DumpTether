using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DumpTether.App.Auth;
using DumpTether.App.Sync;
using DumpTether.App.Tasks;
using DumpTether.App.Templates;
using DumpTether.App.Workspaces;
using DumpTether.Data;
using DumpTether.Domain;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task ConnectCloudAccount_WhenDesktop_StoresProtectedConnection()
    {
        var cloud = new FakeCloudSyncClient();
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        await LoginDesktopAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/sync/cloud-account",
            new ConnectCloudAccountRequest(
                "https://cloud.example",
                "cloud@example.test",
                "correct horse battery staple",
                "test desktop"));

        response.EnsureSuccessStatusCode();
        var account = await response.Content.ReadFromJsonAsync<CloudSyncAccountResponse>();
        var current = await client.GetFromJsonAsync<CloudSyncAccountResponse?>(
            "/api/sync/cloud-account");

        Assert.NotNull(account);
        Assert.Equal("https://cloud.example", account!.CloudApiBaseUrl);
        Assert.Equal("cloud@example.test", account.CloudEmail);
        Assert.True(account.IsConnected);
        Assert.NotNull(current);
        Assert.Equal(account.Id, current!.Id);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        var stored = await dbContext.CloudSyncAccounts.SingleAsync();
        Assert.NotEqual("fake-cloud-session-token", stored.ProtectedSessionToken);
        Assert.DoesNotContain("fake-cloud-session-token", stored.ProtectedSessionToken);
    }

    [Fact]
    public async Task ReconcileCloudWorkspaces_ImportsCloudOnlyBoardAndPullsItsTasks()
    {
        var cloud = new FakeCloudSyncClient();
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        await LoginDesktopAsync(client);
        await ConnectCloudAccountAsync(client);
        var remoteWorkspace = cloud.AddWorkspace("Cloud only", "#A7F3D0");
        cloud.AddTask(
            remoteWorkspace.Id,
            "Visible after reconciliation",
            lastTouchedAt: DateTimeOffset.UtcNow);

        var reconcileResponse = await client.PostAsync(
            "/api/sync/cloud-workspaces/reconcile",
            content: null);

        reconcileResponse.EnsureSuccessStatusCode();
        var reconciliation =
            (await reconcileResponse.Content.ReadFromJsonAsync<ReconcileCloudWorkspacesResponse>())!;
        var importedRoot = Assert.Single(
            reconciliation.Roots,
            root => root.RemoteWorkspaceId == remoteWorkspace.Id);
        Assert.Equal(1, reconciliation.Imported);
        Assert.Equal(SyncRootOrigin.CloudImported, importedRoot.Origin);

        var syncResponse = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{importedRoot.LocalWorkspaceId}/run",
            new SyncWorkspaceWithCloudRequest(
                PushLocalChanges: false,
                PullRemoteChanges: true));
        syncResponse.EnsureSuccessStatusCode();

        using var workspaceRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/tasks");
        workspaceRequest.Headers.Add(
            "X-DumpTether-Workspace-Id",
            importedRoot.LocalWorkspaceId.ToString());
        var tasksResponse = await client.SendAsync(workspaceRequest);
        tasksResponse.EnsureSuccessStatusCode();
        var tasks = (await tasksResponse.Content.ReadFromJsonAsync<List<TaskItemSummaryResponse>>())!;

        Assert.Contains(tasks, task => task.Title == "Visible after reconciliation");
    }

    [Fact]
    public async Task ReconcileCloudWorkspaces_WhenRemoteAccessIsGone_MarksImportedRootRevoked()
    {
        var cloud = new FakeCloudSyncClient();
        var remoteWorkspace = cloud.AddWorkspace("Temporary cloud access");
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        await LoginDesktopAsync(client);
        await ConnectCloudAccountAsync(client);
        cloud.Workspaces.Clear();
        cloud.TasksByWorkspace.Remove(remoteWorkspace.Id);

        var response = await client.PostAsync(
            "/api/sync/cloud-workspaces/reconcile",
            content: null);

        response.EnsureSuccessStatusCode();
        var reconciliation =
            (await response.Content.ReadFromJsonAsync<ReconcileCloudWorkspacesResponse>())!;
        var root = Assert.Single(
            reconciliation.Roots,
            candidate => candidate.RemoteWorkspaceId == remoteWorkspace.Id);

        Assert.Equal(1, reconciliation.AccessRevoked);
        Assert.Equal(SyncRootStatus.AccessRevoked, root.Status);
    }

    [Fact]
    public async Task DisconnectCloudAccount_RevokesRemoteSessionAndErasesLocalToken()
    {
        var cloud = new FakeCloudSyncClient();
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        await LoginDesktopAsync(client);
        await ConnectCloudAccountAsync(client);

        var response = await client.DeleteAsync("/api/sync/cloud-account");

        response.EnsureSuccessStatusCode();
        Assert.Equal(1, cloud.LogoutCount);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        var stored = await dbContext.CloudSyncAccounts.SingleAsync();
        Assert.Equal(string.Empty, stored.ProtectedSessionToken);
        Assert.NotNull(stored.DisconnectedAt);
    }

    [Fact]
    public async Task DisconnectCloudAccount_WhenCloudIsUnavailable_StillErasesLocalToken()
    {
        var cloud = new FakeCloudSyncClient
        {
            LogoutException = new HttpRequestException("Cloud is unavailable.")
        };
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        await LoginDesktopAsync(client);
        await ConnectCloudAccountAsync(client);

        var response = await client.DeleteAsync("/api/sync/cloud-account");

        response.EnsureSuccessStatusCode();
        Assert.Equal(1, cloud.LogoutCount);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        var stored = await dbContext.CloudSyncAccounts.SingleAsync();
        Assert.Equal(string.Empty, stored.ProtectedSessionToken);
        Assert.NotNull(stored.DisconnectedAt);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenConnectedCloudAccount_PushesLocalTask()
    {
        var cloud = new FakeCloudSyncClient();
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;
        await ConnectCloudAccountAsync(client);
        var created = await CreateTaskItemAsync(client, "Push from stored cloud login");

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            new SyncWorkspaceWithCloudRequest());

        response.EnsureSuccessStatusCode();
        var sync = (await response.Content.ReadFromJsonAsync<SyncWorkspaceWithCloudResponse>())!;
        var remoteTask = Assert.Single(cloud.TasksByWorkspace[cloud.Workspaces.Single().Id]);

        Assert.Equal(1, sync.Pushed);
        Assert.Equal(created.Title, remoteTask.Title);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenCloudHasSameNamedBoard_ReusesIt()
    {
        var cloud = new FakeCloudSyncClient();
        var existingRemoteWorkspace = cloud.AddWorkspace("All Tasks");
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;
        await ConnectCloudAccountAsync(client);
        await CreateTaskItemAsync(client, "Push into existing board");

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            new SyncWorkspaceWithCloudRequest());

        response.EnsureSuccessStatusCode();
        var sync = (await response.Content.ReadFromJsonAsync<SyncWorkspaceWithCloudResponse>())!;

        Assert.Single(cloud.Workspaces);
        Assert.Equal(existingRemoteWorkspace.Id, sync.Root.RemoteWorkspaceId);
        Assert.Single(cloud.TasksByWorkspace[existingRemoteWorkspace.Id]);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenCloudHasMatchingDefaultTemplate_ReusesIt()
    {
        var cloud = new FakeCloudSyncClient();
        var existingRemoteWorkspace = cloud.AddWorkspace("All Tasks");
        var existingRemoteTemplate = cloud.AddTemplate(
            "Basic Task",
            new CloudSyncUpsertFieldDefinitionRequest(
                Id: null,
                Name: "Context",
                Type: "LongText",
                Scope: "Header",
                Required: false,
                SortOrder: 0,
                Options: [],
                LayoutRow: 1,
                LayoutColumn: 1,
                LayoutRowSpan: 1,
                LayoutColumnSpan: 2,
                LayoutWeight: 1));
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;
        await ConnectCloudAccountAsync(client);
        await CreateTaskItemAsync(client, "Push with existing default template");

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            new SyncWorkspaceWithCloudRequest());

        response.EnsureSuccessStatusCode();
        var sync = (await response.Content.ReadFromJsonAsync<SyncWorkspaceWithCloudResponse>())!;
        var remoteTask = Assert.Single(cloud.TasksByWorkspace[existingRemoteWorkspace.Id]);

        Assert.Equal(1, sync.Pushed);
        Assert.Equal(0, sync.Failed);
        Assert.Single(cloud.Templates, template => template.Id == existingRemoteTemplate.Id);
        Assert.Equal(existingRemoteTemplate.Id, remoteTask.TaskTemplateId);
        Assert.Contains(
            sync.Messages,
            message => message.Contains("Linked existing cloud template", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenNoConnectedCloudAccount_ReturnsBadRequest()
    {
        var cloud = new FakeCloudSyncClient();
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;
        await CreateTaskItemAsync(client, "No cloud account yet");

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            new SyncWorkspaceWithCloudRequest());
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Connect a cloud account", body);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenDesktop_PushesLocalTask()
    {
        var cloud = new FakeCloudSyncClient();
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;
        var created = await CreateTaskItemAsync(client, "Push me");

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            new SyncWorkspaceWithCloudRequest("https://cloud.example", "cloud-token"));

        response.EnsureSuccessStatusCode();
        var sync = (await response.Content.ReadFromJsonAsync<SyncWorkspaceWithCloudResponse>())!;
        var tasks = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks");

        Assert.Equal(1, sync.Pushed);
        Assert.Equal(0, sync.Pulled);
        Assert.Equal(0, sync.Conflicts);
        Assert.Single(cloud.Workspaces);
        var remoteTask = Assert.Single(cloud.TasksByWorkspace[cloud.Workspaces.Single().Id]);
        Assert.Equal(created.Title, remoteTask.Title);
        Assert.Equal(
            SyncMappingStatus.Synced.ToString(),
            tasks!.Single(task => task.Id == created.Id).SyncState?.Status);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenDesktop_PushesSubtaskAfterParent()
    {
        var cloud = new FakeCloudSyncClient();
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;
        var parent = await CreateTaskItemAsync(client, "Cloud parent");
        var childResponse = await client.PostAsJsonAsync(
            $"/api/tasks/{parent.Id}/subtasks",
            new CreateTaskItemRequest("Cloud child"));
        childResponse.EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            new SyncWorkspaceWithCloudRequest("https://cloud.example", "cloud-token"));

        response.EnsureSuccessStatusCode();
        var sync = (await response.Content.ReadFromJsonAsync<SyncWorkspaceWithCloudResponse>())!;
        var remoteTasks = cloud.TasksByWorkspace[cloud.Workspaces.Single().Id];
        var remoteParent = Assert.Single(remoteTasks, task => task.ParentTaskItemId is null);
        var remoteChild = Assert.Single(remoteTasks, task => task.ParentTaskItemId.HasValue);

        Assert.Equal(2, sync.Pushed);
        Assert.Equal(remoteParent.Id, remoteChild.ParentTaskItemId);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenRemoteCreateSucceededBeforeFailure_RetryDoesNotDuplicateTask()
    {
        var cloud = new FakeCloudSyncClient
        {
            FailAfterNextTaskCreate = true
        };
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;
        var created = await CreateTaskItemAsync(client, "Retry remote create");
        var request = new SyncWorkspaceWithCloudRequest(
            "https://cloud.example",
            "cloud-token");

        var failure = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.PostAsJsonAsync(
                $"/api/sync/workspaces/{workspaceId}/run",
                request));
        Assert.Contains("lost response", failure.Message, StringComparison.OrdinalIgnoreCase);

        var secondResponse = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            request);
        secondResponse.EnsureSuccessStatusCode();
        var second = (await secondResponse.Content.ReadFromJsonAsync<SyncWorkspaceWithCloudResponse>())!;

        Assert.Equal(0, second.Failed);
        var remoteTask = Assert.Single(
            cloud.TasksByWorkspace[cloud.Workspaces.Single().Id],
            task => task.Id == created.Id);
        Assert.Equal(created.Title, remoteTask.Title);
        Assert.Equal(2, cloud.CreateTaskAttempts);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenRequestsOverlap_SerializesWorkspaceSync()
    {
        var cloud = new FakeCloudSyncClient
        {
            PauseNextTaskCreate = true
        };
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;
        var created = await CreateTaskItemAsync(client, "Concurrent sync");
        var request = new SyncWorkspaceWithCloudRequest(
            "https://cloud.example",
            "cloud-token");

        var firstRequest = client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            request);
        await cloud.WaitForPausedTaskCreateAsync();
        var secondRequest = client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            request);

        var prematureCompletion = await Task.WhenAny(
            secondRequest,
            Task.Delay(TimeSpan.FromMilliseconds(100)));
        Assert.NotSame(secondRequest, prematureCompletion);
        Assert.Equal(1, cloud.CreateTaskAttempts);

        cloud.ResumeTaskCreate();
        var responses = await Task.WhenAll(firstRequest, secondRequest);

        foreach (var response in responses)
        {
            response.EnsureSuccessStatusCode();
        }

        Assert.Single(
            cloud.TasksByWorkspace[cloud.Workspaces.Single().Id],
            task => task.Id == created.Id);
        Assert.Equal(1, cloud.CreateTaskAttempts);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenCloudUrlIsInvalid_ReturnsBadRequest()
    {
        var cloud = new FakeCloudSyncClient();
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;
        await CreateTaskItemAsync(client, "Do not sync yet");

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            new SyncWorkspaceWithCloudRequest("https://token@cloud.example", "cloud-token"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("absolute HTTP(S) URL without credentials", body);
        Assert.Empty(cloud.Workspaces);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenRemoteCloudUrlUsesHttp_ReturnsBadRequest()
    {
        var cloud = new FakeCloudSyncClient();
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            new SyncWorkspaceWithCloudRequest("http://cloud.example", "cloud-token"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("must use HTTPS", body);
        Assert.Empty(cloud.Workspaces);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenTaskHasTemplate_PushesTemplateAndHeaderFieldValues()
    {
        var cloud = new FakeCloudSyncClient();
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;
        var template = await CreateTemplateAsync(client);
        var contextField = template.Fields.Single(field => field.Name == "Context");
        using var valueDocument = JsonDocument.Parse("\"Remember toothbrush\"");
        var createTaskResponse = await client.PostAsJsonAsync(
            "/api/tasks",
            new CreateTaskItemRequest(
                "Pack bag",
                template.Id,
                new Dictionary<Guid, JsonElement>
                {
                    [contextField.Id] = valueDocument.RootElement.Clone()
                }));
        createTaskResponse.EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            new SyncWorkspaceWithCloudRequest("https://cloud.example", "cloud-token"));

        response.EnsureSuccessStatusCode();
        var sync = (await response.Content.ReadFromJsonAsync<SyncWorkspaceWithCloudResponse>())!;
        var remoteTemplate = Assert.Single(
            cloud.Templates,
            template => template.Name == "Travel Note");
        var remoteField = Assert.Single(remoteTemplate.Fields);
        var remoteTask = Assert.Single(cloud.TasksByWorkspace[cloud.Workspaces.Single().Id]);
        var remoteFieldValue = Assert.Single(remoteTask.FieldValues!);

        Assert.Equal(1, sync.Pushed);
        Assert.Equal("Travel Note", remoteTemplate.Name);
        Assert.Equal(remoteTemplate.Id, remoteTask.TaskTemplateId);
        Assert.Equal(contextField.Key, remoteField.Key);
        Assert.Equal(remoteField.Id, remoteFieldValue.FieldDefinitionId);
        Assert.Equal("\"Remember toothbrush\"", remoteFieldValue.ValueJson);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenDesktop_PullsRemoteTask()
    {
        var cloud = new FakeCloudSyncClient();
        var remoteWorkspace = cloud.AddWorkspace("Cloud board");
        cloud.AddTask(remoteWorkspace.Id, "Pulled from cloud", lastTouchedAt: DateTimeOffset.UtcNow);
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            new SyncWorkspaceWithCloudRequest(
                "https://cloud.example",
                "cloud-token",
                remoteWorkspace.Id,
                PushLocalChanges: false));

        response.EnsureSuccessStatusCode();
        var sync = (await response.Content.ReadFromJsonAsync<SyncWorkspaceWithCloudResponse>())!;
        var tasks = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks");

        Assert.Equal(0, sync.Pushed);
        Assert.Equal(1, sync.Pulled);
        var pulled = Assert.Single(tasks!, task => task.Title == "Pulled from cloud");
        Assert.Equal(SyncMappingStatus.Synced.ToString(), pulled.SyncState?.Status);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenDesktop_PullsParentBeforeSubtask()
    {
        var cloud = new FakeCloudSyncClient();
        var remoteWorkspace = cloud.AddWorkspace("Cloud board");
        var remoteParent = cloud.AddTask(
            remoteWorkspace.Id,
            "Remote parent",
            lastTouchedAt: DateTimeOffset.UtcNow);
        cloud.AddTask(
            remoteWorkspace.Id,
            "Remote child",
            lastTouchedAt: DateTimeOffset.UtcNow,
            parentTaskItemId: remoteParent.Id);
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            new SyncWorkspaceWithCloudRequest(
                "https://cloud.example",
                "cloud-token",
                remoteWorkspace.Id,
                PushLocalChanges: false));

        response.EnsureSuccessStatusCode();
        var sync = (await response.Content.ReadFromJsonAsync<SyncWorkspaceWithCloudResponse>())!;
        var localTasks = (await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks?includeChildTasks=true"))!;
        var localParent = Assert.Single(localTasks, task => task.ParentTaskItemId is null);
        var localChild = Assert.Single(localTasks, task => task.ParentTaskItemId.HasValue);

        Assert.Equal(2, sync.Pulled);
        Assert.Equal(localParent.Id, localChild.ParentTaskItemId);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenUntemplatedRemoteTaskHasNote_PullsStableNoteOnce()
    {
        var cloud = new FakeCloudSyncClient();
        var remoteWorkspace = cloud.AddWorkspace("Cloud board");
        var remoteNoteId = Guid.NewGuid();
        cloud.AddTask(
            remoteWorkspace.Id,
            "Remote note",
            lastTouchedAt: DateTimeOffset.UtcNow,
            timelineEntries:
            [
                new CloudSyncTimelineEntryResponse(
                    remoteNoteId,
                    "Pulled exactly once.",
                    DateTimeOffset.UtcNow)
            ]);
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;
        var request = new SyncWorkspaceWithCloudRequest(
            "https://cloud.example",
            "cloud-token",
            remoteWorkspace.Id,
            PushLocalChanges: false);

        var firstResponse = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            request);
        var secondResponse = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            request);

        firstResponse.EnsureSuccessStatusCode();
        secondResponse.EnsureSuccessStatusCode();
        var tasks = (await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks"))!;
        var pulled = Assert.Single(tasks, task => task.Title == "Remote note");
        var detail = (await client.GetFromJsonAsync<TaskItemDetailResponse>(
            $"/api/tasks/{pulled.Id}"))!;

        var note = Assert.Single(
            detail.TimelineEntries,
            entry => entry.Id == remoteNoteId);
        Assert.Equal("Pulled exactly once.", note.Details);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenPullOnlyAndMappedRemoteChanged_UpdatesLocalTask()
    {
        var cloud = new FakeCloudSyncClient();
        var remoteWorkspace = cloud.AddWorkspace("Cloud board");
        var remoteTask = cloud.AddTask(
            remoteWorkspace.Id,
            "Original",
            status: "Waiting",
            color: "#FFE58A",
            lastTouchedAt: DateTimeOffset.UtcNow);
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;
        var localTask = await CreateTaskItemAsync(client, "Original");

        var markResponse = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/tasks/{localTask.Id}/synced",
            new MarkTaskItemSyncedRequest(remoteTask.Id, "v1"));
        markResponse.EnsureSuccessStatusCode();

        cloud.ReplaceTask(remoteWorkspace.Id, remoteTask with
        {
            Title = "Cloud title",
            Status = "Follow-up",
            Color = "#A7F3D0",
            LastTouchedAt = DateTimeOffset.UtcNow.AddMinutes(5)
        });

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            new SyncWorkspaceWithCloudRequest(
                "https://cloud.example",
                "cloud-token",
                remoteWorkspace.Id,
                PushLocalChanges: false));

        response.EnsureSuccessStatusCode();
        var sync = (await response.Content.ReadFromJsonAsync<SyncWorkspaceWithCloudResponse>())!;
        var detail = await client.GetFromJsonAsync<TaskItemDetailResponse>($"/api/tasks/{localTask.Id}");

        Assert.Equal(0, sync.Pulled);
        Assert.Equal(1, sync.UpdatedLocal);
        Assert.Equal(0, sync.Conflicts);
        Assert.Equal("Cloud title", detail!.Title);
        Assert.Equal("Follow-up", detail.Status);
        Assert.Equal("#A7F3D0", detail.Color);
        Assert.Equal(SyncMappingStatus.Synced.ToString(), detail.SyncState?.Status);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenRemoteTaskHasTemplate_PullsTemplateAndHeaderFieldValues()
    {
        var cloud = new FakeCloudSyncClient();
        var remoteWorkspace = cloud.AddWorkspace("Cloud board");
        var remoteTemplate = cloud.AddTemplate(
            "Cloud Travel Note",
            new CloudSyncUpsertFieldDefinitionRequest(
                Id: null,
                Name: "Context",
                Type: "LongText",
                Scope: "Header",
                Required: false,
                SortOrder: 0,
                Options: [],
                LayoutRow: 1,
                LayoutColumn: 1,
                LayoutRowSpan: 1,
                LayoutColumnSpan: 1,
                LayoutWeight: 1));
        var remoteField = Assert.Single(remoteTemplate.Fields);
        cloud.AddTask(
            remoteWorkspace.Id,
            "Pulled templated task",
            taskTemplateId: remoteTemplate.Id,
            lastTouchedAt: DateTimeOffset.UtcNow,
            fieldValues: new Dictionary<Guid, string>
            {
                [remoteField.Id] = "\"Buy sunscreen\""
            });
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            new SyncWorkspaceWithCloudRequest(
                "https://cloud.example",
                "cloud-token",
                remoteWorkspace.Id,
                PushLocalChanges: false));

        response.EnsureSuccessStatusCode();
        var sync = (await response.Content.ReadFromJsonAsync<SyncWorkspaceWithCloudResponse>())!;
        var tasks = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks");
        var pulled = Assert.Single(tasks!, task => task.Title == "Pulled templated task");
        var detail = await client.GetFromJsonAsync<TaskItemDetailResponse>($"/api/tasks/{pulled.Id}");
        var localField = Assert.Single(detail!.Template!.Fields);
        var localFieldValue = Assert.Single(detail.FieldValues);

        Assert.Equal(1, sync.Pulled);
        Assert.Contains(sync.Messages, message => message.Contains("Imported cloud template", StringComparison.Ordinal));
        Assert.Equal("Cloud Travel Note", detail.Template.Name);
        Assert.Equal("Context", localField.Name);
        Assert.Equal(localField.Id, localFieldValue.FieldDefinitionId);
        Assert.Equal("\"Buy sunscreen\"", localFieldValue.ValueJson);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenMappedRemoteFieldValueChanged_UpdatesLocalFieldValue()
    {
        var cloud = new FakeCloudSyncClient();
        var remoteWorkspace = cloud.AddWorkspace("Cloud board");
        var remoteTemplate = cloud.AddTemplate(
            "Cloud Travel Note",
            new CloudSyncUpsertFieldDefinitionRequest(
                Id: null,
                Name: "Context",
                Type: "LongText",
                Scope: "Header",
                Required: false,
                SortOrder: 0,
                Options: [],
                LayoutRow: 1,
                LayoutColumn: 1,
                LayoutRowSpan: 1,
                LayoutColumnSpan: 1,
                LayoutWeight: 1));
        var remoteField = Assert.Single(remoteTemplate.Fields);
        var remoteTask = cloud.AddTask(
            remoteWorkspace.Id,
            "Pulled templated task",
            taskTemplateId: remoteTemplate.Id,
            lastTouchedAt: DateTimeOffset.UtcNow,
            fieldValues: new Dictionary<Guid, string>
            {
                [remoteField.Id] = "\"Old context\""
            });
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;

        var firstSyncResponse = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            new SyncWorkspaceWithCloudRequest(
                "https://cloud.example",
                "cloud-token",
                remoteWorkspace.Id,
                PushLocalChanges: false));
        firstSyncResponse.EnsureSuccessStatusCode();
        var tasks = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks");
        var localTask = Assert.Single(tasks!, task => task.Title == "Pulled templated task");

        cloud.ReplaceTask(remoteWorkspace.Id, remoteTask with
        {
            FieldValues =
            [
                new CloudSyncFieldValueResponse(
                    remoteField.Id,
                    "\"New context\"",
                    DateTimeOffset.UtcNow.AddMinutes(5))
            ],
            LastTouchedAt = DateTimeOffset.UtcNow.AddMinutes(5)
        });

        var secondSyncResponse = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            new SyncWorkspaceWithCloudRequest(
                "https://cloud.example",
                "cloud-token",
                remoteWorkspace.Id,
                PushLocalChanges: false));

        secondSyncResponse.EnsureSuccessStatusCode();
        var sync = (await secondSyncResponse.Content.ReadFromJsonAsync<SyncWorkspaceWithCloudResponse>())!;
        var detail = await client.GetFromJsonAsync<TaskItemDetailResponse>($"/api/tasks/{localTask.Id}");
        var fieldValue = Assert.Single(detail!.FieldValues);

        Assert.Equal(0, sync.Pulled);
        Assert.Equal(1, sync.UpdatedLocal);
        Assert.Equal("\"New context\"", fieldValue.ValueJson);
        Assert.Equal(SyncMappingStatus.Synced.ToString(), detail.SyncState?.Status);
    }

    [Fact]
    public async Task SyncWorkspaceWithCloud_WhenBothSidesChanged_MarksConflict()
    {
        var cloud = new FakeCloudSyncClient();
        var remoteWorkspace = cloud.AddWorkspace("Cloud board");
        var remoteTask = cloud.AddTask(
            remoteWorkspace.Id,
            "Original",
            lastTouchedAt: DateTimeOffset.UtcNow);
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop",
            cloudSyncClient: cloud);
        using var client = factory.CreateClient();
        var login = await LoginDesktopAsync(client);
        var workspaceId = login.Workspaces.Single().Id;
        var localTask = await CreateTaskItemAsync(client, "Original");

        var markResponse = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/tasks/{localTask.Id}/synced",
            new MarkTaskItemSyncedRequest(remoteTask.Id, "v1"));
        markResponse.EnsureSuccessStatusCode();

        await Task.Delay(20);
        await PatchTaskItemAsync(client, localTask.Id, new { title = "Local edit" });
        cloud.ReplaceTask(remoteWorkspace.Id, remoteTask with
        {
            Title = "Cloud edit",
            LastTouchedAt = DateTimeOffset.UtcNow.AddMinutes(5)
        });

        var response = await client.PostAsJsonAsync(
            $"/api/sync/workspaces/{workspaceId}/run",
            new SyncWorkspaceWithCloudRequest(
                "https://cloud.example",
                "cloud-token",
                remoteWorkspace.Id));

        response.EnsureSuccessStatusCode();
        var sync = (await response.Content.ReadFromJsonAsync<SyncWorkspaceWithCloudResponse>())!;
        var tasks = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks");

        Assert.Equal(1, sync.Conflicts);
        Assert.Contains(sync.Messages, message => message.Contains("Conflict", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            SyncMappingStatus.Conflict.ToString(),
            tasks!.Single(task => task.Id == localTask.Id).SyncState?.Status);
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

    private static async Task<CloudSyncAccountResponse> ConnectCloudAccountAsync(
        HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/sync/cloud-account",
            new ConnectCloudAccountRequest(
                "https://cloud.example",
                "cloud@example.test",
                "correct horse battery staple",
                "test desktop"));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CloudSyncAccountResponse>())!;
    }

    private static async Task<TaskTemplateDetailResponse> CreateTemplateAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/templates",
            new CreateTaskTemplateRequest(
                "Travel Note",
                [
                    new UpsertFieldDefinitionRequest(
                        Id: null,
                        Name: "Context",
                        Type: "LongText",
                        Scope: "Header",
                        Required: false,
                        SortOrder: 0)
                ]));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TaskTemplateDetailResponse>())!;
    }

    private static async Task<TaskItemDetailResponse> PatchTaskItemAsync(
        HttpClient client,
        Guid id,
        object request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/tasks/{id}")
        {
            Content = JsonContent.Create(request)
        };
        var response = await client.SendAsync(message);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>())!;
    }

    private sealed class FakeCloudSyncClient : ICloudSyncClient
    {
        private readonly TaskCompletionSource<bool> _pausedTaskCreateEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _resumeTaskCreate =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CloudSyncUserResponse _user = new(
            Guid.NewGuid(),
            "cloud@example.test",
            "Cloud User");
        private int _createTaskAttempts;
        private int _pauseTaskCreateConsumed;

        public bool FailAfterNextTaskCreate { get; set; }

        public bool PauseNextTaskCreate { get; set; }

        public int CreateTaskAttempts => Volatile.Read(ref _createTaskAttempts);

        public List<CloudSyncWorkspaceResponse> Workspaces { get; } = [];

        public List<CloudSyncTaskTemplateResponse> Templates { get; } = [];

        public Dictionary<Guid, List<CloudSyncTaskResponse>> TasksByWorkspace { get; } = [];

        public int LogoutCount { get; private set; }

        public Exception? LogoutException { get; init; }

        public Task<CloudSyncLoginResponse> LoginAsync(
            string cloudApiBaseUrl,
            CloudSyncLoginRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new CloudSyncLoginResponse(
                _user,
                "fake-cloud-session-token",
                DateTimeOffset.UtcNow.AddDays(30)));
        }

        public Task LogoutAsync(
            CloudSyncConnection connection,
            CancellationToken cancellationToken)
        {
            LogoutCount++;
            if (LogoutException is not null)
            {
                throw LogoutException;
            }

            return Task.CompletedTask;
        }

        public Task<CloudSyncUserResponse> GetCurrentUserAsync(
            CloudSyncConnection connection,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_user);
        }

        public Task<IReadOnlyList<CloudSyncWorkspaceResponse>> ListWorkspacesAsync(
            CloudSyncConnection connection,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<CloudSyncWorkspaceResponse>>(
                Workspaces.ToList());
        }

        public Task<CloudSyncWorkspaceResponse> CreateWorkspaceAsync(
            CloudSyncConnection connection,
            CloudSyncCreateWorkspaceRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(AddWorkspace(request.Name, request.Color));
        }

        public Task<CloudSyncWorkspaceResponse> UpdateWorkspaceAsync(
            CloudSyncConnection connection,
            Guid workspaceId,
            CloudSyncUpdateWorkspaceRequest request,
            CancellationToken cancellationToken)
        {
            var index = Workspaces.FindIndex(workspace => workspace.Id == workspaceId);
            if (index < 0)
            {
                throw new InvalidOperationException("Workspace was not found.");
            }

            var workspace = Workspaces[index];
            var updated = workspace with
            {
                Name = request.Name ?? workspace.Name,
                Color = request.Color,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            Workspaces[index] = updated;

            return Task.FromResult(updated);
        }

        public Task<IReadOnlyList<CloudSyncTaskTemplateResponse>> ListTaskTemplatesAsync(
            CloudSyncConnection connection,
            CancellationToken cancellationToken)
        {
            EnsureBuiltInTemplates();
            return Task.FromResult<IReadOnlyList<CloudSyncTaskTemplateResponse>>(Templates.ToList());
        }

        public Task<CloudSyncTaskTemplateResponse?> GetTaskTemplateAsync(
            CloudSyncConnection connection,
            Guid taskTemplateId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Templates.FirstOrDefault(template => template.Id == taskTemplateId));
        }

        public Task<CloudSyncTaskTemplateResponse> CreateTaskTemplateAsync(
            CloudSyncConnection connection,
            CloudSyncCreateTaskTemplateRequest request,
            CancellationToken cancellationToken)
        {
            var created = CreateTemplate(Guid.NewGuid(), request.Name, request.Fields, request.Layout);
            Templates.Add(created);

            return Task.FromResult(created);
        }

        public Task<CloudSyncTaskTemplateResponse> UpdateTaskTemplateAsync(
            CloudSyncConnection connection,
            Guid taskTemplateId,
            CloudSyncUpdateTaskTemplateRequest request,
            CancellationToken cancellationToken)
        {
            var index = Templates.FindIndex(template => template.Id == taskTemplateId);
            if (index < 0)
            {
                throw new InvalidOperationException("Template was not found.");
            }

            var updated = CreateTemplate(taskTemplateId, request.Name, request.Fields, request.Layout);
            Templates[index] = updated;

            return Task.FromResult(updated);
        }

        public Task<IReadOnlyList<CloudSyncTaskResponse>> ListTasksAsync(
            CloudSyncConnection connection,
            Guid workspaceId,
            CancellationToken cancellationToken)
        {
            var tasks = TasksByWorkspace.TryGetValue(workspaceId, out var value)
                ? value.ToList()
                : [];

            return Task.FromResult<IReadOnlyList<CloudSyncTaskResponse>>(tasks);
        }

        public async Task<CloudSyncTaskResponse> CreateTaskAsync(
            CloudSyncConnection connection,
            Guid workspaceId,
            CloudSyncCreateTaskRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _createTaskAttempts);
            if (PauseNextTaskCreate &&
                Interlocked.CompareExchange(ref _pauseTaskCreateConsumed, 1, 0) == 0)
            {
                _pausedTaskCreateEntered.TrySetResult(true);
                await _resumeTaskCreate.Task.WaitAsync(cancellationToken);
            }

            var created = AddTask(
                workspaceId,
                request.Title,
                request.TaskTemplateId,
                request.Status,
                request.Category,
                request.Color,
                request.FollowUpAt,
                DateTimeOffset.UtcNow,
                request.FieldValues,
                request.ClientGeneratedId,
                MapTimelineEntries(request.TimelineEntries),
                request.ParentTaskItemId);

            if (FailAfterNextTaskCreate)
            {
                FailAfterNextTaskCreate = false;
                throw new HttpRequestException(
                    "Simulated lost response after cloud task creation.");
            }

            return created;
        }

        public Task WaitForPausedTaskCreateAsync()
        {
            return _pausedTaskCreateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void ResumeTaskCreate()
        {
            _resumeTaskCreate.TrySetResult(true);
        }

        public Task<CloudSyncTaskResponse> UpdateTaskAsync(
            CloudSyncConnection connection,
            Guid workspaceId,
            Guid taskItemId,
            CloudSyncUpdateTaskRequest request,
            CancellationToken cancellationToken)
        {
            var task = TasksByWorkspace[workspaceId].Single(task => task.Id == taskItemId);
            var updated = task with
            {
                Title = request.Title ?? task.Title,
                TaskTemplateId = request.TaskTemplateId ?? task.TaskTemplateId,
                Status = request.Status,
                Category = request.Category,
                Color = request.Color,
                FollowUpAt = request.FollowUpAt,
                FieldValues = request.FieldValues is null
                    ? task.FieldValues
                    : MapFieldValues(request.FieldValues),
                LastTouchedAt = DateTimeOffset.UtcNow
            };

            ReplaceTask(workspaceId, updated);

            return Task.FromResult(updated);
        }

        public CloudSyncWorkspaceResponse AddWorkspace(string name, string? color = null)
        {
            var workspace = new CloudSyncWorkspaceResponse(Guid.NewGuid(), name, color);
            Workspaces.Add(workspace);
            TasksByWorkspace[workspace.Id] = [];

            return workspace;
        }

        public CloudSyncTaskTemplateResponse AddTemplate(
            string name,
            params CloudSyncUpsertFieldDefinitionRequest[] fields)
        {
            var template = CreateTemplate(
                Guid.NewGuid(),
                name,
                fields,
                new CloudSyncTaskTemplateLayoutRequest(
                    [new CloudSyncTaskTemplateLayoutRowRequest(1, [1], 132)],
                    [new CloudSyncTaskTemplateLayoutRowRequest(1, [1], 132)]));
            Templates.Add(template);

            return template;
        }

        private void EnsureBuiltInTemplates()
        {
            var basic = Templates.FirstOrDefault(template =>
                template.BuiltInKind == TaskTemplateBuiltInKind.Basic.ToString() ||
                (string.IsNullOrWhiteSpace(template.BuiltInKind) && template.Name == "Basic Task"));
            ReplaceTemplate(
                basic,
                CreateTemplate(
                    basic?.Id ?? Guid.NewGuid(),
                    "Basic Task",
                    [new CloudSyncUpsertFieldDefinitionRequest(
                        Id: null,
                        Name: "Context",
                        Type: "LongText",
                        Scope: "Header",
                        Required: false,
                        SortOrder: 0,
                        Options: [],
                        LayoutRow: 1,
                        LayoutColumn: 1,
                        LayoutRowSpan: 1,
                        LayoutColumnSpan: 1,
                        LayoutWeight: 1)],
                    new CloudSyncTaskTemplateLayoutRequest(
                        [new CloudSyncTaskTemplateLayoutRowRequest(1, [1], 190)],
                        [new CloudSyncTaskTemplateLayoutRowRequest(1, [1], 90)]),
                    TaskTemplateBuiltInKind.Basic));

            var todo = Templates.FirstOrDefault(template =>
                template.BuiltInKind == TaskTemplateBuiltInKind.Todo.ToString() ||
                (string.IsNullOrWhiteSpace(template.BuiltInKind) && template.Name == "ToDo Task"));
            ReplaceTemplate(
                todo,
                CreateTemplate(
                    todo?.Id ?? Guid.NewGuid(),
                    "ToDo Task",
                    [
                        new CloudSyncUpsertFieldDefinitionRequest(
                            null, "Description", "LongText", "Header", false, 0, [], 1, 1, 1, 1, 1),
                        new CloudSyncUpsertFieldDefinitionRequest(
                            null, "Item", "Text", "Entry", true, 0, [], 1, 1, 1, 1, 4),
                        new CloudSyncUpsertFieldDefinitionRequest(
                            null, "Done", "Checkbox", "Entry", false, 1, [], 1, 2, 1, 1, 1)
                    ],
                    new CloudSyncTaskTemplateLayoutRequest(
                        [new CloudSyncTaskTemplateLayoutRowRequest(1, [1], 190)],
                        [new CloudSyncTaskTemplateLayoutRowRequest(1, [4, 1], 90)]),
                    TaskTemplateBuiltInKind.Todo));
        }

        private void ReplaceTemplate(
            CloudSyncTaskTemplateResponse? existing,
            CloudSyncTaskTemplateResponse replacement)
        {
            if (existing is null)
            {
                Templates.Add(replacement);
                return;
            }

            Templates[Templates.IndexOf(existing)] = replacement;
        }

        public CloudSyncTaskResponse AddTask(
            Guid workspaceId,
            string title,
            Guid? taskTemplateId = null,
            string? status = null,
            string? category = null,
            string? color = null,
            DateTimeOffset? followUpAt = null,
            DateTimeOffset? lastTouchedAt = null,
            IReadOnlyDictionary<Guid, string>? fieldValues = null,
            Guid? id = null,
            IReadOnlyList<CloudSyncTimelineEntryResponse>? timelineEntries = null,
            Guid? parentTaskItemId = null)
        {
            var createdAt = lastTouchedAt ?? DateTimeOffset.UtcNow;
            var taskId = id ?? Guid.NewGuid();
            if (TasksByWorkspace.TryGetValue(workspaceId, out var existingTasks))
            {
                var existingTask = existingTasks.FirstOrDefault(task => task.Id == taskId);
                if (existingTask is not null)
                {
                    return existingTask;
                }
            }

            var task = new CloudSyncTaskResponse(
                taskId,
                workspaceId,
                taskTemplateId,
                title,
                status,
                category,
                color,
                createdAt,
                lastTouchedAt ?? createdAt,
                followUpAt,
                ArchivedAt: null,
                MapFieldValues(fieldValues),
                timelineEntries,
                parentTaskItemId);

            TasksByWorkspace[workspaceId].Add(task);

            return task;
        }

        public void ReplaceTask(Guid workspaceId, CloudSyncTaskResponse task)
        {
            var tasks = TasksByWorkspace[workspaceId];
            var index = tasks.FindIndex(existing => existing.Id == task.Id);
            if (index < 0)
            {
                tasks.Add(task);
                return;
            }

            tasks[index] = task;
        }

        private static CloudSyncTaskTemplateResponse CreateTemplate(
            Guid id,
            string name,
            IReadOnlyList<CloudSyncUpsertFieldDefinitionRequest> fields,
            CloudSyncTaskTemplateLayoutRequest layout,
            TaskTemplateBuiltInKind builtInKind = TaskTemplateBuiltInKind.None)
        {
            return new CloudSyncTaskTemplateResponse(
                id,
                name,
                DateTimeOffset.UtcNow,
                new CloudSyncTaskTemplateLayoutResponse(
                    layout.Header
                        .Select(row => new CloudSyncTaskTemplateLayoutRowResponse(
                            row.Row,
                            row.ColumnWeights,
                            row.Height))
                        .ToList(),
                    layout.Entry
                        .Select(row => new CloudSyncTaskTemplateLayoutRowResponse(
                            row.Row,
                            row.ColumnWeights,
                            row.Height))
                        .ToList()),
                fields
                    .Select(field => new CloudSyncFieldDefinitionResponse(
                        field.Id ?? Guid.NewGuid(),
                        GenerateKey(field.Name),
                        field.Name,
                        field.Type,
                        field.Scope,
                        field.Required,
                        field.SortOrder,
                        field.Options,
                        field.LayoutRow,
                        field.LayoutColumn,
                        field.LayoutRowSpan,
                        field.LayoutColumnSpan,
                        field.LayoutWeight))
                    .ToList(),
                builtInKind == TaskTemplateBuiltInKind.None ? null : builtInKind.ToString(),
                builtInKind != TaskTemplateBuiltInKind.None);
        }

        private static IReadOnlyList<CloudSyncFieldValueResponse> MapFieldValues(
            IReadOnlyDictionary<Guid, string>? fieldValues)
        {
            if (fieldValues is null || fieldValues.Count == 0)
            {
                return [];
            }

            return fieldValues
                .Select(value => new CloudSyncFieldValueResponse(
                    value.Key,
                    value.Value,
                    DateTimeOffset.UtcNow))
                .ToList();
        }

        private static IReadOnlyList<CloudSyncTimelineEntryResponse> MapTimelineEntries(
            IReadOnlyList<CloudSyncTimelineEntryRequest>? timelineEntries)
        {
            if (timelineEntries is null || timelineEntries.Count == 0)
            {
                return [];
            }

            return timelineEntries
                .Select(entry => new CloudSyncTimelineEntryResponse(
                    entry.ClientGeneratedId,
                    entry.Note,
                    DateTimeOffset.UtcNow,
                    MapFieldValues(entry.FieldValues)))
                .ToList();
        }

        private static string GenerateKey(string name)
        {
            var keyCharacters = name
                .Trim()
                .ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '_')
                .ToArray();
            var key = string.Join(
                '_',
                new string(keyCharacters)
                    .Split('_', StringSplitOptions.RemoveEmptyEntries));

            return string.IsNullOrWhiteSpace(key) ? "field" : key;
        }
    }
}
