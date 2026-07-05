using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DumpTether.App.Auth;
using DumpTether.App.Sync;
using DumpTether.App.Tasks;
using DumpTether.App.Templates;
using DumpTether.App.Workspaces;
using DumpTether.Domain;
using System.Text.Json;
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
        var remoteTemplate = Assert.Single(cloud.Templates);
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
        private readonly CloudSyncUserResponse _user = new(
            Guid.NewGuid(),
            "cloud@example.test",
            "Cloud User");

        public List<CloudSyncWorkspaceResponse> Workspaces { get; } = [];

        public List<CloudSyncTaskTemplateResponse> Templates { get; } = [];

        public Dictionary<Guid, List<CloudSyncTaskResponse>> TasksByWorkspace { get; } = [];

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

        public Task<IReadOnlyList<CloudSyncTaskTemplateResponse>> ListTaskTemplatesAsync(
            CloudSyncConnection connection,
            CancellationToken cancellationToken)
        {
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

        public Task<CloudSyncTaskResponse> CreateTaskAsync(
            CloudSyncConnection connection,
            Guid workspaceId,
            CloudSyncCreateTaskRequest request,
            CancellationToken cancellationToken)
        {
            var created = AddTask(
                workspaceId,
                request.Title,
                request.TaskTemplateId,
                request.Status,
                request.Category,
                request.Color,
                request.FollowUpAt,
                DateTimeOffset.UtcNow,
                request.FieldValues);

            return Task.FromResult(created);
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

        public CloudSyncTaskResponse AddTask(
            Guid workspaceId,
            string title,
            Guid? taskTemplateId = null,
            string? status = null,
            string? category = null,
            string? color = null,
            DateTimeOffset? followUpAt = null,
            DateTimeOffset? lastTouchedAt = null,
            IReadOnlyDictionary<Guid, string>? fieldValues = null)
        {
            var createdAt = lastTouchedAt ?? DateTimeOffset.UtcNow;
            var task = new CloudSyncTaskResponse(
                Guid.NewGuid(),
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
                MapFieldValues(fieldValues));

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
            CloudSyncTaskTemplateLayoutRequest layout)
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
                    .ToList());
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
