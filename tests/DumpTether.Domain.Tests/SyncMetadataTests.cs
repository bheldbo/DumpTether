using DumpTether.Domain;
using Xunit;

namespace DumpTether.Domain.Tests;

public sealed class SyncMetadataTests
{
    [Fact]
    public void CreateLocalRoot_StartsAsLocalOnly()
    {
        var workspaceId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 7, 1, 14, 30, 0, TimeSpan.Zero);

        var syncRoot = SyncRoot.CreateLocal(workspaceId, "desktop-device", createdAt);

        Assert.NotEqual(Guid.Empty, syncRoot.Id);
        Assert.Equal(workspaceId, syncRoot.LocalWorkspaceId);
        Assert.Equal("desktop-device", syncRoot.DeviceId);
        Assert.Equal(SyncRootStatus.LocalOnly, syncRoot.Status);
        Assert.Null(syncRoot.RemoteWorkspaceId);
        Assert.Null(syncRoot.CloudUserId);
        Assert.Null(syncRoot.LastSyncedAt);
    }

    [Fact]
    public void LinkRemote_PreventsRelinkingDifferentRemoteBoard()
    {
        var syncRoot = SyncRoot.CreateLocal(
            Guid.NewGuid(),
            "desktop-device",
            new DateTimeOffset(2026, 7, 1, 14, 30, 0, TimeSpan.Zero));
        var remoteWorkspaceId = Guid.NewGuid();
        var cloudUserId = Guid.NewGuid();

        syncRoot.LinkRemote(
            remoteWorkspaceId,
            cloudUserId,
            syncRoot.CreatedAt.AddMinutes(1));

        Assert.Equal(remoteWorkspaceId, syncRoot.RemoteWorkspaceId);
        Assert.Equal(cloudUserId, syncRoot.CloudUserId);
        Assert.Equal(SyncRootStatus.Linked, syncRoot.Status);
        Assert.Throws<InvalidOperationException>(() => syncRoot.LinkRemote(
            Guid.NewGuid(),
            cloudUserId,
            syncRoot.CreatedAt.AddMinutes(2)));
    }

    [Fact]
    public void Mapping_LinkRemote_PreventsRelinkingDifferentRemoteEntity()
    {
        var mapping = SyncMapping.CreateLocal(
            Guid.NewGuid(),
            SyncEntityType.TaskItem,
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 1, 14, 30, 0, TimeSpan.Zero));
        var remoteTaskId = Guid.NewGuid();

        mapping.LinkRemote(
            remoteTaskId,
            "v1",
            mapping.CreatedAt.AddMinutes(1));

        Assert.Equal(remoteTaskId, mapping.RemoteId);
        Assert.Equal("v1", mapping.LastRemoteVersion);
        Assert.Equal(SyncMappingStatus.Synced, mapping.Status);
        Assert.Throws<InvalidOperationException>(() => mapping.LinkRemote(
            Guid.NewGuid(),
            "v2",
            mapping.CreatedAt.AddMinutes(2)));
    }

    [Fact]
    public void Mapping_MarkSyncFailed_StoresFailureEvidence()
    {
        var mapping = SyncMapping.CreateLocal(
            Guid.NewGuid(),
            SyncEntityType.TaskItem,
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 1, 14, 30, 0, TimeSpan.Zero));
        var attemptedAt = mapping.CreatedAt.AddMinutes(5);

        mapping.MarkSyncFailed("Cloud API was unavailable.", attemptedAt);

        Assert.Equal(SyncMappingStatus.SyncFailed, mapping.Status);
        Assert.Equal("Cloud API was unavailable.", mapping.LastError);
        Assert.Equal(attemptedAt, mapping.LastAttemptedAt);
        Assert.Equal(attemptedAt, mapping.UpdatedAt);
        Assert.Null(mapping.LastSyncedAt);
    }

    [Theory]
    [InlineData("http://localhost:55868", "http://localhost:55868")]
    [InlineData("http://127.0.0.1:55868/", "http://127.0.0.1:55868")]
    [InlineData("https://tasks.example.com/api/", "https://tasks.example.com/api")]
    public void CloudApiBaseUrl_AllowsHttpsAndLoopbackDevelopment(
        string value,
        string expected)
    {
        Assert.Equal(expected, CloudSyncAccount.NormalizeCloudApiBaseUrl(value));
    }

    [Theory]
    [InlineData("http://tasks.example.com")]
    [InlineData("http://192.168.1.20:55868")]
    public void CloudApiBaseUrl_RejectsInsecureRemoteServer(string value)
    {
        Assert.Throws<ArgumentException>(
            () => CloudSyncAccount.NormalizeCloudApiBaseUrl(value));
    }

    [Fact]
    public void CloudAccount_DisconnectErasesProtectedSessionToken()
    {
        var now = new DateTimeOffset(2026, 7, 1, 14, 30, 0, TimeSpan.Zero);
        var account = CloudSyncAccount.Create(
            Guid.NewGuid(),
            "https://tasks.example.com",
            Guid.NewGuid(),
            "user@example.com",
            "Test User",
            "protected-session-token",
            now.AddDays(30),
            now);

        account.Disconnect(now.AddMinutes(5));

        Assert.Equal(string.Empty, account.ProtectedSessionToken);
        Assert.Equal(now.AddMinutes(5), account.DisconnectedAt);
        Assert.False(account.HasUsableSession(now.AddMinutes(5)));
    }
}
