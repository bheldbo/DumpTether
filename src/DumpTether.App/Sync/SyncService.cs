using System.ComponentModel.DataAnnotations;
using DumpTether.App.Auth;
using DumpTether.App.Tasks;
using DumpTether.App.Workspaces;
using DumpTether.Domain;

namespace DumpTether.App.Sync;

internal sealed class SyncService : ISyncService
{
    private const string DefaultLocalDesktopDeviceId = "local-desktop";

    private readonly IAuthRepository _authRepository;
    private readonly IClock _clock;
    private readonly ICurrentUserSessionProvider _currentUserSessionProvider;
    private readonly ISyncRepository _syncRepository;
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly IWorkspaceRepository _workspaceRepository;

    public SyncService(
        IAuthRepository authRepository,
        IClock clock,
        ICurrentUserSessionProvider currentUserSessionProvider,
        ISyncRepository syncRepository,
        ITaskItemRepository taskItemRepository,
        IWorkspaceRepository workspaceRepository)
    {
        _authRepository = authRepository;
        _clock = clock;
        _currentUserSessionProvider = currentUserSessionProvider;
        _syncRepository = syncRepository;
        _taskItemRepository = taskItemRepository;
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

    public async Task EnsureLocalTaskMappingAsync(
        Guid workspaceId,
        Guid taskItemId,
        CancellationToken cancellationToken)
    {
        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);
        if (currentSession is null || !IsDesktopSession(currentSession))
        {
            return;
        }

        await RequireWorkspaceMembershipAsync(
            workspaceId,
            currentSession.UserId,
            requireOwner: false,
            cancellationToken);

        var syncRoot = await EnsureLocalRootAsync(
            workspaceId,
            DefaultLocalDesktopDeviceId,
            cancellationToken);

        var existingMapping = await _syncRepository.GetMappingAsync(
            syncRoot.Id,
            SyncEntityType.TaskItem,
            taskItemId,
            trackChanges: false,
            cancellationToken);

        if (existingMapping is not null)
        {
            return;
        }

        await _syncRepository.AddMappingAsync(
            SyncMapping.CreateLocal(
                syncRoot.Id,
                SyncEntityType.TaskItem,
                taskItemId,
                _clock.UtcNow),
            cancellationToken);
        await _syncRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, TaskSyncStateResponse>> ListTaskSyncStatesAsync(
        Guid workspaceId,
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken cancellationToken)
    {
        if (taskItemIds.Count == 0)
        {
            return new Dictionary<Guid, TaskSyncStateResponse>();
        }

        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);
        if (currentSession is null || !IsDesktopSession(currentSession))
        {
            return new Dictionary<Guid, TaskSyncStateResponse>();
        }

        var root = await _syncRepository.GetRootByLocalWorkspaceAsync(
            workspaceId,
            trackChanges: false,
            cancellationToken);

        if (root is null)
        {
            return taskItemIds
                .Distinct()
                .ToDictionary(
                    taskItemId => taskItemId,
                    _ => CreateLocalOnlyState());
        }

        var mappings = await _syncRepository.ListMappingsAsync(
            root.Id,
            SyncEntityType.TaskItem,
            taskItemIds,
            cancellationToken);
        var mappedStates = mappings.ToDictionary(
            mapping => mapping.LocalId,
            MapTaskSyncState);

        foreach (var taskItemId in taskItemIds.Distinct())
        {
            mappedStates.TryAdd(taskItemId, CreateLocalOnlyState());
        }

        return mappedStates;
    }

    public async Task<TaskSyncStateResponse> MarkTaskItemSyncedAsync(
        Guid workspaceId,
        Guid taskItemId,
        MarkTaskItemSyncedRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        if (!IsDesktopSession(currentSession))
        {
            throw new UnauthorizedAccessException("Desktop sync session is required.");
        }

        await RequireWorkspaceMembershipAsync(
            workspaceId,
            currentSession.UserId,
            requireOwner: false,
            cancellationToken);

        var mapping = await EnsureLocalTaskMappingForUpdateAsync(
            workspaceId,
            taskItemId,
            cancellationToken);
        mapping.LinkRemote(
            request.RemoteTaskItemId,
            request.RemoteVersion,
            _clock.UtcNow);
        await _syncRepository.SaveChangesAsync(cancellationToken);

        return MapTaskSyncState(mapping);
    }

    public async Task<TaskSyncStateResponse> MarkTaskItemSyncFailedAsync(
        Guid workspaceId,
        Guid taskItemId,
        MarkTaskItemSyncFailedRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        if (!IsDesktopSession(currentSession))
        {
            throw new UnauthorizedAccessException("Desktop sync session is required.");
        }

        await RequireWorkspaceMembershipAsync(
            workspaceId,
            currentSession.UserId,
            requireOwner: false,
            cancellationToken);

        var mapping = await EnsureLocalTaskMappingForUpdateAsync(
            workspaceId,
            taskItemId,
            cancellationToken);
        mapping.MarkSyncFailed(request.Error, _clock.UtcNow);
        await _syncRepository.SaveChangesAsync(cancellationToken);

        return MapTaskSyncState(mapping);
    }

    private async Task<SyncMapping> EnsureLocalTaskMappingForUpdateAsync(
        Guid workspaceId,
        Guid taskItemId,
        CancellationToken cancellationToken)
    {
        var taskItem = await _taskItemRepository.GetByIdAsync(
            taskItemId,
            workspaceId,
            projectId: null,
            trackChanges: false,
            cancellationToken);

        if (taskItem is null)
        {
            throw new ValidationException("Task was not found.");
        }

        var syncRoot = await EnsureLocalRootAsync(
            workspaceId,
            DefaultLocalDesktopDeviceId,
            cancellationToken);

        var existingMapping = await _syncRepository.GetMappingAsync(
            syncRoot.Id,
            SyncEntityType.TaskItem,
            taskItemId,
            trackChanges: true,
            cancellationToken);

        if (existingMapping is not null)
        {
            return existingMapping;
        }

        var mapping = SyncMapping.CreateLocal(
            syncRoot.Id,
            SyncEntityType.TaskItem,
            taskItemId,
            _clock.UtcNow);
        await _syncRepository.AddMappingAsync(mapping, cancellationToken);

        return mapping;
    }

    private async Task<SyncRoot> EnsureLocalRootAsync(
        Guid workspaceId,
        string deviceId,
        CancellationToken cancellationToken)
    {
        var syncRoot = await _syncRepository.GetRootByLocalWorkspaceAsync(
            workspaceId,
            trackChanges: true,
            cancellationToken);

        if (syncRoot is not null)
        {
            return syncRoot;
        }

        syncRoot = SyncRoot.CreateLocal(
            workspaceId,
            deviceId,
            _clock.UtcNow);
        await _syncRepository.AddRootAsync(syncRoot, cancellationToken);

        return syncRoot;
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

    private static bool IsDesktopSession(CurrentUserSession currentSession)
    {
        return currentSession.SessionType is UserSessionType.DesktopLocal or UserSessionType.DesktopCloud;
    }

    private static TaskSyncStateResponse CreateLocalOnlyState()
    {
        return new TaskSyncStateResponse(
            SyncMappingStatus.LocalOnly.ToString(),
            RemoteId: null,
            LastRemoteVersion: null,
            LastAttemptedAt: null,
            LastSyncedAt: null,
            LastError: null);
    }

    private static TaskSyncStateResponse MapTaskSyncState(SyncMapping mapping)
    {
        return new TaskSyncStateResponse(
            mapping.Status.ToString(),
            mapping.RemoteId,
            mapping.LastRemoteVersion,
            mapping.LastAttemptedAt,
            mapping.LastSyncedAt,
            mapping.LastError);
    }
}
