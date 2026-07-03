namespace DumpTether.App.Sync;

internal sealed class NoOpCloudSyncClient : ICloudSyncClient
{
    public Task<CloudSyncUserResponse> GetCurrentUserAsync(
        CloudSyncConnection connection,
        CancellationToken cancellationToken)
    {
        throw CreateMissingClientException();
    }

    public Task<IReadOnlyList<CloudSyncWorkspaceResponse>> ListWorkspacesAsync(
        CloudSyncConnection connection,
        CancellationToken cancellationToken)
    {
        throw CreateMissingClientException();
    }

    public Task<CloudSyncWorkspaceResponse> CreateWorkspaceAsync(
        CloudSyncConnection connection,
        CloudSyncCreateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateMissingClientException();
    }

    public Task<IReadOnlyList<CloudSyncTaskResponse>> ListTasksAsync(
        CloudSyncConnection connection,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        throw CreateMissingClientException();
    }

    public Task<CloudSyncTaskResponse> CreateTaskAsync(
        CloudSyncConnection connection,
        Guid workspaceId,
        CloudSyncCreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateMissingClientException();
    }

    public Task<CloudSyncTaskResponse> UpdateTaskAsync(
        CloudSyncConnection connection,
        Guid workspaceId,
        Guid taskItemId,
        CloudSyncUpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateMissingClientException();
    }

    private static InvalidOperationException CreateMissingClientException()
    {
        return new InvalidOperationException("Cloud sync HTTP client is not configured for this runtime.");
    }
}
