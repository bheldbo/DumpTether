namespace DumpTether.App.Sync;

internal sealed class NoOpCloudSyncClient : ICloudSyncClient
{
    public Task<CloudSyncLoginResponse> LoginAsync(
        string cloudApiBaseUrl,
        CloudSyncLoginRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateMissingClientException();
    }

    public Task LogoutAsync(
        CloudSyncConnection connection,
        CancellationToken cancellationToken)
    {
        throw CreateMissingClientException();
    }

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

    public Task<CloudSyncWorkspaceResponse> UpdateWorkspaceAsync(
        CloudSyncConnection connection,
        Guid workspaceId,
        CloudSyncUpdateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateMissingClientException();
    }

    public Task<IReadOnlyList<CloudSyncTaskTemplateResponse>> ListTaskTemplatesAsync(
        CloudSyncConnection connection,
        CancellationToken cancellationToken)
    {
        throw CreateMissingClientException();
    }

    public Task<CloudSyncTaskTemplateResponse?> GetTaskTemplateAsync(
        CloudSyncConnection connection,
        Guid taskTemplateId,
        CancellationToken cancellationToken)
    {
        throw CreateMissingClientException();
    }

    public Task<CloudSyncTaskTemplateResponse> CreateTaskTemplateAsync(
        CloudSyncConnection connection,
        CloudSyncCreateTaskTemplateRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateMissingClientException();
    }

    public Task<CloudSyncTaskTemplateResponse> UpdateTaskTemplateAsync(
        CloudSyncConnection connection,
        Guid taskTemplateId,
        CloudSyncUpdateTaskTemplateRequest request,
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
