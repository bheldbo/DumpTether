namespace DumpTether.Domain;

public sealed class SyncMapping
{
    private SyncMapping()
    {
    }

    private SyncMapping(
        Guid id,
        Guid syncRootId,
        SyncEntityType entityType,
        Guid localId,
        DateTimeOffset createdAt)
    {
        Id = id;
        SyncRootId = syncRootId;
        EntityType = entityType;
        LocalId = localId;
        Status = SyncMappingStatus.LocalOnly;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid SyncRootId { get; private set; }

    public SyncEntityType EntityType { get; private set; }

    public Guid LocalId { get; private set; }

    public Guid? RemoteId { get; private set; }

    public string? LastRemoteVersion { get; private set; }

    public SyncMappingStatus Status { get; private set; } = SyncMappingStatus.LocalOnly;

    public string? LastError { get; private set; }

    public DateTimeOffset? LastAttemptedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? LastSyncedAt { get; private set; }

    public static SyncMapping CreateLocal(
        Guid syncRootId,
        SyncEntityType entityType,
        Guid localId,
        DateTimeOffset createdAt)
    {
        DomainGuards.NotEmpty(syncRootId, nameof(syncRootId));
        DomainGuards.NotEmpty(localId, nameof(localId));

        return new SyncMapping(
            Guid.NewGuid(),
            syncRootId,
            entityType,
            localId,
            createdAt);
    }

    public void LinkRemote(
        Guid remoteId,
        string? remoteVersion,
        DateTimeOffset syncedAt)
    {
        DomainGuards.NotEmpty(remoteId, nameof(remoteId));

        if (RemoteId.HasValue && RemoteId.Value != remoteId)
        {
            throw new InvalidOperationException(
                "This local entity is already linked to a different cloud entity.");
        }

        RemoteId = remoteId;
        LastRemoteVersion = NormalizeRemoteVersion(remoteVersion);
        Status = SyncMappingStatus.Synced;
        LastError = null;
        LastAttemptedAt = syncedAt;
        LastSyncedAt = syncedAt;
        UpdatedAt = syncedAt;
    }

    public void MarkConflict(DateTimeOffset occurredAt)
    {
        Status = SyncMappingStatus.Conflict;
        LastAttemptedAt = occurredAt;
        UpdatedAt = occurredAt;
    }

    public void MarkDeleted(DateTimeOffset occurredAt)
    {
        Status = SyncMappingStatus.Deleted;
        LastAttemptedAt = occurredAt;
        LastError = null;
        UpdatedAt = occurredAt;
    }

    public void MarkSyncFailed(string error, DateTimeOffset attemptedAt)
    {
        var normalizedError = DomainGuards.NotBlank(error, nameof(error));

        Status = SyncMappingStatus.SyncFailed;
        LastAttemptedAt = attemptedAt;
        LastError = normalizedError.Length <= 1000
            ? normalizedError
            : normalizedError[..1000];
        UpdatedAt = attemptedAt;
    }

    private static string? NormalizeRemoteVersion(string? remoteVersion)
    {
        var normalized = DomainGuards.OptionalTrimmed(remoteVersion);

        if (normalized is not null && normalized.Length > 200)
        {
            throw new ArgumentException(
                "Remote version cannot be longer than 200 characters.",
                nameof(remoteVersion));
        }

        return normalized;
    }
}
