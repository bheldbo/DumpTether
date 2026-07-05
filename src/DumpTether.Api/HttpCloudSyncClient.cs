using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DumpTether.App.Auth;
using DumpTether.App.Sync;
using DumpTether.App.Tasks;
using DumpTether.App.Templates;
using DumpTether.App.Workspaces;

namespace DumpTether.Api;

internal sealed class HttpCloudSyncClient : ICloudSyncClient
{
    private const string WorkspaceHeaderName = "X-DumpTether-Workspace-Id";
    private readonly HttpClient _httpClient;

    public HttpCloudSyncClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CloudSyncLoginResponse> LoginAsync(
        string cloudApiBaseUrl,
        CloudSyncLoginRequest request,
        CancellationToken cancellationToken)
    {
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUri(cloudApiBaseUrl, "/api/auth/login"))
        {
            Content = JsonContent.Create(new LoginUserRequest(
                request.Email,
                request.Password,
                string.IsNullOrWhiteSpace(request.DeviceName)
                    ? "DumpTether desktop"
                    : request.DeviceName.Trim()))
        };

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Cloud login failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        var login = await response.Content.ReadFromJsonAsync<LoginUserResponse>(
            cancellationToken: cancellationToken);
        if (login is null || string.IsNullOrWhiteSpace(login.SessionToken))
        {
            throw new InvalidOperationException("Cloud login returned an empty response.");
        }

        return new CloudSyncLoginResponse(
            new CloudSyncUserResponse(
                login.User.Id,
                login.User.Email,
                login.User.DisplayName),
            login.SessionToken,
            login.ExpiresAt);
    }

    public async Task<CloudSyncUserResponse> GetCurrentUserAsync(
        CloudSyncConnection connection,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync<CurrentUserResponse>(
            connection,
            HttpMethod.Get,
            "/api/auth/me",
            workspaceId: null,
            body: null,
            cancellationToken);

        return new CloudSyncUserResponse(
            response.User.Id,
            response.User.Email,
            response.User.DisplayName);
    }

    public async Task<IReadOnlyList<CloudSyncWorkspaceResponse>> ListWorkspacesAsync(
        CloudSyncConnection connection,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync<IReadOnlyList<WorkspaceResponse>>(
            connection,
            HttpMethod.Get,
            "/api/workspaces",
            workspaceId: null,
            body: null,
            cancellationToken);

        return response
            .Select(workspace => new CloudSyncWorkspaceResponse(
                workspace.Id,
                workspace.Name,
                workspace.Color))
            .ToList();
    }

    public async Task<CloudSyncWorkspaceResponse> CreateWorkspaceAsync(
        CloudSyncConnection connection,
        CloudSyncCreateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync<WorkspaceResponse>(
            connection,
            HttpMethod.Post,
            "/api/workspaces",
            workspaceId: null,
            body: new CreateWorkspaceRequest(request.Name, request.Color),
            cancellationToken);

        return new CloudSyncWorkspaceResponse(
            response.Id,
            response.Name,
            response.Color);
    }

    public async Task<IReadOnlyList<CloudSyncTaskTemplateResponse>> ListTaskTemplatesAsync(
        CloudSyncConnection connection,
        CancellationToken cancellationToken)
    {
        var summaries = await SendAsync<IReadOnlyList<TaskTemplateSummaryResponse>>(
            connection,
            HttpMethod.Get,
            "/api/templates",
            workspaceId: null,
            body: null,
            cancellationToken);
        var templates = new List<CloudSyncTaskTemplateResponse>();

        foreach (var summary in summaries)
        {
            var template = await GetTaskTemplateAsync(connection, summary.Id, cancellationToken);
            if (template is not null)
            {
                templates.Add(template);
            }
        }

        return templates;
    }

    public async Task<CloudSyncTaskTemplateResponse?> GetTaskTemplateAsync(
        CloudSyncConnection connection,
        Guid taskTemplateId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await SendAsync<TaskTemplateDetailResponse>(
                connection,
                HttpMethod.Get,
                $"/api/templates/{taskTemplateId}",
                workspaceId: null,
                body: null,
                cancellationToken);

            return MapTemplate(response);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public async Task<CloudSyncTaskTemplateResponse> CreateTaskTemplateAsync(
        CloudSyncConnection connection,
        CloudSyncCreateTaskTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync<TaskTemplateDetailResponse>(
            connection,
            HttpMethod.Post,
            "/api/templates",
            workspaceId: null,
            body: new CreateTaskTemplateRequest(
                request.Name,
                request.Fields.Select(MapFieldRequest).ToList(),
                MapLayoutRequest(request.Layout)),
            cancellationToken);

        return MapTemplate(response);
    }

    public async Task<CloudSyncTaskTemplateResponse> UpdateTaskTemplateAsync(
        CloudSyncConnection connection,
        Guid taskTemplateId,
        CloudSyncUpdateTaskTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync<TaskTemplateDetailResponse>(
            connection,
            HttpMethod.Patch,
            $"/api/templates/{taskTemplateId}",
            workspaceId: null,
            body: new UpdateTaskTemplateRequest(
                request.Name,
                request.Fields.Select(MapFieldRequest).ToList(),
                MapLayoutRequest(request.Layout)),
            cancellationToken);

        return MapTemplate(response);
    }

    public async Task<IReadOnlyList<CloudSyncTaskResponse>> ListTasksAsync(
        CloudSyncConnection connection,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var summaries = await SendAsync<IReadOnlyList<TaskItemSummaryResponse>>(
            connection,
            HttpMethod.Get,
            "/api/tasks?scope=All",
            workspaceId,
            body: null,
            cancellationToken);
        var tasks = new List<CloudSyncTaskResponse>();

        foreach (var summary in summaries)
        {
            var detail = await SendAsync<TaskItemDetailResponse>(
                connection,
                HttpMethod.Get,
                $"/api/tasks/{summary.Id}",
                workspaceId,
                body: null,
                cancellationToken);
            tasks.Add(MapTask(detail));
        }

        return tasks;
    }

    public async Task<CloudSyncTaskResponse> CreateTaskAsync(
        CloudSyncConnection connection,
        Guid workspaceId,
        CloudSyncCreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var created = await SendAsync<TaskItemDetailResponse>(
            connection,
            HttpMethod.Post,
            "/api/tasks",
            workspaceId,
            new CreateTaskItemRequest(
                request.Title,
                request.TaskTemplateId,
                BuildFieldValuePayload(request.FieldValues),
                ProjectId: null,
                request.Category),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Status) ||
            !string.IsNullOrWhiteSpace(request.Color) ||
            request.FollowUpAt.HasValue)
        {
            created = await SendAsync<TaskItemDetailResponse>(
                connection,
                HttpMethod.Patch,
                $"/api/tasks/{created.Id}",
                workspaceId,
                new UpdateTaskItemRequest(
                    Title: null,
                    request.Status,
                    request.Category,
                    request.Color,
                    request.FollowUpAt,
                    BuildFieldValuePayload(request.FieldValues),
                    ProjectId: null),
                cancellationToken);
        }

        return MapTask(created);
    }

    public async Task<CloudSyncTaskResponse> UpdateTaskAsync(
        CloudSyncConnection connection,
        Guid workspaceId,
        Guid taskItemId,
        CloudSyncUpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync<TaskItemDetailResponse>(
            connection,
            HttpMethod.Patch,
            $"/api/tasks/{taskItemId}",
            workspaceId,
            new UpdateTaskItemRequest(
                request.Title,
                request.Status,
                request.Category,
                request.Color,
                request.FollowUpAt,
                BuildFieldValuePayload(request.FieldValues),
                ProjectId: null),
            cancellationToken);

        return MapTask(response);
    }

    private static Dictionary<Guid, JsonElement>? BuildFieldValuePayload(
        IReadOnlyDictionary<Guid, string>? fieldValues)
    {
        if (fieldValues is null || fieldValues.Count == 0)
        {
            return null;
        }

        var payload = new Dictionary<Guid, JsonElement>();
        foreach (var (fieldDefinitionId, valueJson) in fieldValues)
        {
            using var document = JsonDocument.Parse(valueJson);
            payload[fieldDefinitionId] = document.RootElement.Clone();
        }

        return payload;
    }

    private static UpsertFieldDefinitionRequest MapFieldRequest(
        CloudSyncUpsertFieldDefinitionRequest field)
    {
        return new UpsertFieldDefinitionRequest(
            field.Id,
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
            field.LayoutWeight);
    }

    private static TaskTemplateLayoutRequest MapLayoutRequest(
        CloudSyncTaskTemplateLayoutRequest layout)
    {
        return new TaskTemplateLayoutRequest(
            layout.Header
                .Select(row => new TaskTemplateLayoutRowRequest(row.Row, row.ColumnWeights, row.Height))
                .ToList(),
            layout.Entry
                .Select(row => new TaskTemplateLayoutRowRequest(row.Row, row.ColumnWeights, row.Height))
                .ToList());
    }

    private static CloudSyncTaskTemplateResponse MapTemplate(TaskTemplateDetailResponse template)
    {
        return new CloudSyncTaskTemplateResponse(
            template.Id,
            template.Name,
            template.UpdatedAt,
            new CloudSyncTaskTemplateLayoutResponse(
                template.Layout.Header
                    .Select(row => new CloudSyncTaskTemplateLayoutRowResponse(
                        row.Row,
                        row.ColumnWeights,
                        row.Height))
                    .ToList(),
                template.Layout.Entry
                    .Select(row => new CloudSyncTaskTemplateLayoutRowResponse(
                        row.Row,
                        row.ColumnWeights,
                        row.Height))
                    .ToList()),
            template.Fields
                .Select(field => new CloudSyncFieldDefinitionResponse(
                    field.Id,
                    field.Key,
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

    private async Task<TResponse> SendAsync<TResponse>(
        CloudSyncConnection connection,
        HttpMethod method,
        string path,
        Guid? workspaceId,
        object? body,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, BuildUri(connection.BaseUrl, path));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            NormalizeSessionToken(connection.SessionToken));

        if (workspaceId.HasValue)
        {
            request.Headers.TryAddWithoutValidation(WorkspaceHeaderName, workspaceId.Value.ToString());
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Cloud API request failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        var value = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
        return value ?? throw new InvalidOperationException("Cloud API returned an empty response.");
    }

    private static Uri BuildUri(string baseUrl, string path)
    {
        var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
        return new Uri(normalizedBaseUrl, path);
    }

    private static Uri NormalizeBaseUrl(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            throw new ArgumentException("Cloud API base URL must be an absolute HTTP(S) URL without credentials.");
        }

        return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri($"{uri.AbsoluteUri}/");
    }

    private static string NormalizeSessionToken(string sessionToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            throw new ArgumentException("Cloud session token is required.");
        }

        return sessionToken.Trim();
    }

    private static CloudSyncTaskResponse MapTask(TaskItemSummaryResponse taskItem)
    {
        return new CloudSyncTaskResponse(
            taskItem.Id,
            taskItem.WorkspaceId,
            taskItem.TaskTemplateId,
            taskItem.Title,
            taskItem.Status,
            taskItem.Category,
            taskItem.Color,
            taskItem.CreatedAt,
            taskItem.LastTouchedAt,
            taskItem.FollowUpAt,
            taskItem.ArchivedAt,
            FieldValues: []);
    }

    private static CloudSyncTaskResponse MapTask(TaskItemDetailResponse taskItem)
    {
        return new CloudSyncTaskResponse(
            taskItem.Id,
            taskItem.WorkspaceId,
            taskItem.TaskTemplateId,
            taskItem.Title,
            taskItem.Status,
            taskItem.Category,
            taskItem.Color,
            taskItem.CreatedAt,
            taskItem.LastTouchedAt,
            taskItem.FollowUpAt,
            taskItem.ArchivedAt,
            taskItem.FieldValues
                .Select(value => new CloudSyncFieldValueResponse(
                    value.FieldDefinitionId,
                    value.ValueJson,
                    value.UpdatedAt))
                .ToList());
    }
}
