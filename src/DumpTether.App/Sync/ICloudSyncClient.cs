namespace DumpTether.App.Sync;

public interface ICloudSyncClient
{
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
