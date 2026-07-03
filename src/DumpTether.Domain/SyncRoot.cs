namespace DumpTether.Domain;

public sealed class SyncRoot
{
    private readonly List<SyncMapping> _mappings = [];

    private SyncRoot()
    {
    }

    private SyncRoot(
        Guid id,
        Guid localWorkspaceId,
        string deviceId,
        DateTimeOffset createdAt)
    {
        Id = id;
        LocalWorkspaceId = localWorkspaceId;
        DeviceId = NormalizeDeviceId(deviceId);
        Status = SyncRootStatus.LocalOnly;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid LocalWorkspaceId { get; private set; }

    public Guid? RemoteWorkspaceId { get; private set; }

    public Guid? CloudUserId { get; private set; }

    public string DeviceId { get; private set; } = string.Empty;

    public SyncRootStatus Status { get; private set; } = SyncRootStatus.LocalOnly;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? LastSyncedAt { get; private set; }

    public IReadOnlyCollection<SyncMapping> Mappings => _mappings.AsReadOnly();

    public static SyncRoot CreateLocal(
        Guid localWorkspaceId,
        string deviceId,
        DateTimeOffset createdAt)
    {
        DomainGuards.NotEmpty(localWorkspaceId, nameof(localWorkspaceId));

        return new SyncRoot(
            Guid.NewGuid(),
            localWorkspaceId,
            deviceId,
            createdAt);
    }

    public void LinkRemote(
        Guid remoteWorkspaceId,
        Guid cloudUserId,
        DateTimeOffset linkedAt)
    {
        DomainGuards.NotEmpty(remoteWorkspaceId, nameof(remoteWorkspaceId));
        DomainGuards.NotEmpty(cloudUserId, nameof(cloudUserId));

        if (RemoteWorkspaceId.HasValue && RemoteWorkspaceId.Value != remoteWorkspaceId)
        {
            throw new InvalidOperationException(
                "This local board is already linked to a different cloud board.");
        }

        if (CloudUserId.HasValue && CloudUserId.Value != cloudUserId)
        {
            throw new InvalidOperationException(
                "This local board is already linked to a different cloud user.");
        }

        RemoteWorkspaceId = remoteWorkspaceId;
        CloudUserId = cloudUserId;
        Status = SyncRootStatus.Linked;
        UpdatedAt = linkedAt;
    }

    public void MarkSynced(DateTimeOffset syncedAt)
    {
        if (!RemoteWorkspaceId.HasValue)
        {
            throw new InvalidOperationException("Only linked sync roots can be marked synced.");
        }

        LastSyncedAt = syncedAt;
        Status = SyncRootStatus.Linked;
        UpdatedAt = syncedAt;
    }

    public void MarkConflict(DateTimeOffset occurredAt)
    {
        Status = SyncRootStatus.Conflict;
        UpdatedAt = occurredAt;
    }

    private static string NormalizeDeviceId(string deviceId)
    {
        var normalized = DomainGuards.NotBlank(deviceId, nameof(deviceId));

        if (normalized.Length > 128)
        {
            throw new ArgumentException("Device id cannot be longer than 128 characters.", nameof(deviceId));
        }

        return normalized;
    }
}
