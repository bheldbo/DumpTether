using System.ComponentModel.DataAnnotations;
using DumpTether.App.Auth;
using DumpTether.App.Tasks;
using DumpTether.App.Workspaces;
using DumpTether.Domain;

namespace DumpTether.App.Sync;

internal sealed class SyncService : ISyncService
{
    private readonly IAuthRepository _authRepository;
    private readonly IClock _clock;
    private readonly ICurrentUserSessionProvider _currentUserSessionProvider;
    private readonly ISyncRepository _syncRepository;
    private readonly IWorkspaceRepository _workspaceRepository;

    public SyncService(
        IAuthRepository authRepository,
        IClock clock,
        ICurrentUserSessionProvider currentUserSessionProvider,
        ISyncRepository syncRepository,
        IWorkspaceRepository workspaceRepository)
    {
        _authRepository = authRepository;
        _clock = clock;
        _currentUserSessionProvider = currentUserSessionProvider;
        _syncRepository = syncRepository;
        _workspaceRepository = workspaceRepository;
    }

    public async Task<IReadOnlyList<SyncRootResponse>> ListWorkspaceRootsAsync(
        CancellationToken cancellationToken)
    {
        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        var memberships = await _authRepository.ListWorkspacesForUserAsync(
            currentSession.UserId,
            cancellationToken);
        var workspaceIds = memberships
            .Where(membership => membership.AccessKind == WorkspaceAccessKinds.Membership)
            .Select(membership => membership.Workspace.Id)
            .ToArray();

        if (workspaceIds.Length == 0)
        {
            return [];
        }

        var roots = await _syncRepository.ListRootsForLocalWorkspacesAsync(
            workspaceIds,
            cancellationToken);

        return roots
            .OrderBy(root => root.CreatedAt)
            .Select(MapRoot)
            .ToList();
    }

    public async Task<SyncRootResponse> EnsureWorkspaceRootAsync(
        EnsureWorkspaceSyncRootRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        await RequireWorkspaceMembershipAsync(
            request.LocalWorkspaceId,
            currentSession.UserId,
            requireOwner: false,
            cancellationToken);

        var existingRoot = await _syncRepository.GetRootByLocalWorkspaceAsync(
            request.LocalWorkspaceId,
            trackChanges: false,
            cancellationToken);

        if (existingRoot is not null)
        {
            return MapRoot(existingRoot);
        }

        var syncRoot = SyncRoot.CreateLocal(
            request.LocalWorkspaceId,
            request.DeviceId,
            _clock.UtcNow);
        await _syncRepository.AddRootAsync(syncRoot, cancellationToken);
        await _syncRepository.SaveChangesAsync(cancellationToken);

        return MapRoot(syncRoot);
    }

    public async Task<SyncRootResponse> LinkWorkspaceRootAsync(
        LinkWorkspaceSyncRootRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        await RequireWorkspaceMembershipAsync(
            request.LocalWorkspaceId,
            currentSession.UserId,
            requireOwner: true,
            cancellationToken);

        var existingRemoteRoot = await _syncRepository.GetRootByRemoteWorkspaceAsync(
            request.RemoteWorkspaceId,
            request.CloudUserId,
            trackChanges: false,
            cancellationToken);

        if (existingRemoteRoot is not null &&
            existingRemoteRoot.LocalWorkspaceId != request.LocalWorkspaceId)
        {
            throw new ValidationException("That cloud board is already linked to another local board.");
        }

        var syncRoot = await _syncRepository.GetRootByLocalWorkspaceAsync(
            request.LocalWorkspaceId,
            trackChanges: true,
            cancellationToken);

        if (syncRoot is null)
        {
            syncRoot = SyncRoot.CreateLocal(
                request.LocalWorkspaceId,
                request.DeviceId,
                _clock.UtcNow);
            await _syncRepository.AddRootAsync(syncRoot, cancellationToken);
        }

        syncRoot.LinkRemote(
            request.RemoteWorkspaceId,
            request.CloudUserId,
            _clock.UtcNow);
        await _syncRepository.SaveChangesAsync(cancellationToken);

        return MapRoot(syncRoot);
    }

    private async Task RequireWorkspaceMembershipAsync(
        Guid workspaceId,
        Guid userId,
        bool requireOwner,
        CancellationToken cancellationToken)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id is required.", nameof(workspaceId));
        }

        var membership = await _workspaceRepository.GetMembershipAsync(
            workspaceId,
            userId,
            trackChanges: false,
            cancellationToken);

        if (membership is null)
        {
            throw new UnauthorizedAccessException("Workspace membership is required.");
        }

        if (requireOwner && membership.Role != WorkspaceMembershipRole.Owner)
        {
            throw new UnauthorizedAccessException("Workspace owner role is required.");
        }
    }

    private async Task<CurrentUserSession> RequireCurrentSessionAsync(
        CancellationToken cancellationToken)
    {
        return await _currentUserSessionProvider.GetCurrentAsync(cancellationToken) ??
            throw new UnauthorizedAccessException("Authentication is required.");
    }

    private static SyncRootResponse MapRoot(SyncRoot syncRoot)
    {
        return new SyncRootResponse(
            syncRoot.Id,
            syncRoot.LocalWorkspaceId,
            syncRoot.RemoteWorkspaceId,
            syncRoot.CloudUserId,
            syncRoot.DeviceId,
            syncRoot.Status,
            syncRoot.CreatedAt,
            syncRoot.UpdatedAt,
            syncRoot.LastSyncedAt);
    }
}
