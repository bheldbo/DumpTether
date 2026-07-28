namespace DumpTether.App.Sync;

public interface ICloudSyncClient
{
    Task<CloudSyncLoginResponse> LoginAsync(
        string cloudApiBaseUrl,
        CloudSyncLoginRequest request,
        CancellationToken cancellationToken);

    Task LogoutAsync(
        CloudSyncConnection connection,
        CancellationToken cancellationToken);

    Task<CloudSyncUserResponse> GetCurrentUserAsync(
        CloudSyncConnection connection,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CloudSyncWorkspaceResponse>> ListWorkspacesAsync(
        CloudSyncConnection connection,
        CancellationToken cancellationToken);

    Task<CloudSyncWorkspaceResponse> CreateWorkspaceAsync(
        CloudSyncConnection connection,
        CloudSyncCreateWorkspaceRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CloudSyncTaskTemplateResponse>> ListTaskTemplatesAsync(
        CloudSyncConnection connection,
        CancellationToken cancellationToken);

    Task<CloudSyncTaskTemplateResponse?> GetTaskTemplateAsync(
        CloudSyncConnection connection,
        Guid taskTemplateId,
        CancellationToken cancellationToken);

    Task<CloudSyncTaskTemplateResponse> CreateTaskTemplateAsync(
        CloudSyncConnection connection,
        CloudSyncCreateTaskTemplateRequest request,
        CancellationToken cancellationToken);

    Task<CloudSyncTaskTemplateResponse> UpdateTaskTemplateAsync(
        CloudSyncConnection connection,
        Guid taskTemplateId,
        CloudSyncUpdateTaskTemplateRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CloudSyncTaskResponse>> ListTasksAsync(
        CloudSyncConnection connection,
        Guid workspaceId,
        CancellationToken cancellationToken);

    Task<CloudSyncTaskResponse> CreateTaskAsync(
        CloudSyncConnection connection,
        Guid workspaceId,
        CloudSyncCreateTaskRequest request,
        CancellationToken cancellationToken);

    Task<CloudSyncTaskResponse> UpdateTaskAsync(
        CloudSyncConnection connection,
        Guid workspaceId,
        Guid taskItemId,
        CloudSyncUpdateTaskRequest request,
        CancellationToken cancellationToken);
}
