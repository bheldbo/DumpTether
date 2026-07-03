using System.Net.Http.Headers;
using System.Net.Http.Json;
using DumpTether.App.Auth;
using DumpTether.App.Sync;
using DumpTether.App.Tasks;
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

    public async Task<IReadOnlyList<CloudSyncTaskResponse>> ListTasksAsync(
        CloudSyncConnection connection,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync<IReadOnlyList<TaskItemSummaryResponse>>(
            connection,
            HttpMethod.Get,
            "/api/tasks?scope=All",
            workspaceId,
            body: null,
            cancellationToken);

        return response.Select(MapTask).ToList();
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
                TaskTemplateId: null,
                FieldValues: null,
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
                    FieldValues: null,
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
                FieldValues: null,
                ProjectId: null),
            cancellationToken);

        return MapTask(response);
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
            taskItem.ArchivedAt);
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
            taskItem.ArchivedAt);
    }
}
