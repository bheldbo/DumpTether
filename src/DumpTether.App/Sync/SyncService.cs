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
    private readonly ICloudSyncClient _cloudSyncClient;
    private readonly IClock _clock;
    private readonly ICurrentUserSessionProvider _currentUserSessionProvider;
    private readonly ISyncRepository _syncRepository;
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly IWorkspaceRepository _workspaceRepository;

    public SyncService(
        IAuthRepository authRepository,
        ICloudSyncClient cloudSyncClient,
        IClock clock,
        ICurrentUserSessionProvider currentUserSessionProvider,
        ISyncRepository syncRepository,
        ITaskItemRepository taskItemRepository,
        IWorkspaceRepository workspaceRepository)
    {
        _authRepository = authRepository;
        _cloudSyncClient = cloudSyncClient;
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

    public async Task<SyncWorkspaceWithCloudResponse> SyncWorkspaceWithCloudAsync(
        Guid workspaceId,
        SyncWorkspaceWithCloudRequest request,
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
            requireOwner: true,
            cancellationToken);

        var localWorkspace = await _workspaceRepository.GetByIdAsync(workspaceId, cancellationToken) ??
            throw new ValidationException("Workspace was not found.");
        var connection = CreateCloudConnection(request);
        var cloudUser = await _cloudSyncClient.GetCurrentUserAsync(connection, cancellationToken);
        var now = _clock.UtcNow;
        var root = await EnsureLocalRootAsync(
            workspaceId,
            DefaultLocalDesktopDeviceId,
            cancellationToken);
        var remoteWorkspace = await ResolveRemoteWorkspaceAsync(
            connection,
            localWorkspace,
            root,
            request.RemoteWorkspaceId,
            cancellationToken);
        root.LinkRemote(remoteWorkspace.Id, cloudUser.Id, now);

        var localTasks = await ListLocalTasksForSyncAsync(workspaceId, now, cancellationToken);
        var mappings = (await _syncRepository.ListMappingsForRootAsync(
                root.Id,
                SyncEntityType.TaskItem,
                trackChanges: true,
                cancellationToken))
            .ToDictionary(mapping => mapping.LocalId);
        var remoteTasks = (await _cloudSyncClient.ListTasksAsync(
                connection,
                remoteWorkspace.Id,
                cancellationToken))
            .ToDictionary(task => task.Id);
        var remoteIdsAlreadyMapped = mappings.Values
            .Where(mapping => mapping.RemoteId.HasValue)
            .Select(mapping => mapping.RemoteId!.Value)
            .ToHashSet();

        var stats = new SyncWorkspaceStats();
        var messages = new List<string>();

        if (request.PushLocalChanges)
        {
            foreach (var localTask in localTasks)
            {
                await PushLocalTaskAsync(
                    connection,
                    remoteWorkspace.Id,
                    root,
                    localTask,
                    mappings,
                    remoteTasks,
                    stats,
                    messages,
                    cancellationToken);
            }

            remoteIdsAlreadyMapped = mappings.Values
                .Where(mapping => mapping.RemoteId.HasValue)
                .Select(mapping => mapping.RemoteId!.Value)
                .ToHashSet();
        }

        if (request.PullRemoteChanges)
        {
            foreach (var remoteTask in remoteTasks.Values)
            {
                if (remoteIdsAlreadyMapped.Contains(remoteTask.Id))
                {
                    continue;
                }

                await PullNewRemoteTaskAsync(
                    root,
                    workspaceId,
                    remoteTask,
                    mappings,
                    stats,
                    messages,
                    cancellationToken);
            }
        }

        if (stats.Conflicts > 0)
        {
            root.MarkConflict(now);
        }
        else if (root.RemoteWorkspaceId.HasValue)
        {
            root.MarkSynced(now);
        }

        await _syncRepository.SaveChangesAsync(cancellationToken);

        var taskStates = await ListTaskSyncStatesAsync(
            workspaceId,
            mappings.Keys.ToArray(),
            cancellationToken);

        return new SyncWorkspaceWithCloudResponse(
            MapRoot(root),
            taskStates.Values.ToList(),
            stats.Pushed,
            stats.Pulled,
            stats.UpdatedLocal,
            stats.UpdatedRemote,
            stats.Conflicts,
            stats.Failed,
            messages);
    }

    private static CloudSyncConnection CreateCloudConnection(SyncWorkspaceWithCloudRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CloudApiBaseUrl))
        {
            throw new ArgumentException("Cloud API base URL is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.CloudSessionToken))
        {
            throw new ArgumentException("Cloud session token is required.", nameof(request));
        }

        return new CloudSyncConnection(
            request.CloudApiBaseUrl.Trim(),
            request.CloudSessionToken.Trim());
    }

    private async Task<CloudSyncWorkspaceResponse> ResolveRemoteWorkspaceAsync(
        CloudSyncConnection connection,
        Workspace localWorkspace,
        SyncRoot root,
        Guid? requestedRemoteWorkspaceId,
        CancellationToken cancellationToken)
    {
        var remoteWorkspaceId = requestedRemoteWorkspaceId ?? root.RemoteWorkspaceId;
        if (remoteWorkspaceId.HasValue)
        {
            var existingRemoteWorkspace = (await _cloudSyncClient.ListWorkspacesAsync(
                    connection,
                    cancellationToken))
                .FirstOrDefault(workspace => workspace.Id == remoteWorkspaceId.Value);

            return existingRemoteWorkspace ??
                throw new ValidationException("Selected cloud board was not found for this cloud user.");
        }

        return await _cloudSyncClient.CreateWorkspaceAsync(
            connection,
            new CloudSyncCreateWorkspaceRequest(
                localWorkspace.Name,
                localWorkspace.Color),
            cancellationToken);
    }

    private async Task<IReadOnlyList<TaskItem>> ListLocalTasksForSyncAsync(
        Guid workspaceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await _taskItemRepository.ListAsync(
            new TaskItemQuery(
                workspaceId,
                ProjectId: null,
                Status: null,
                Category: null,
                Color: null,
                TaskItemArchiveFilter.All,
                TaskItemFollowUpFilter.None,
                NotViewedSinceDays: null,
                NotTouchedSinceDays: null,
                Text: null,
                SharedWith: null,
                SharedAccessUserId: null,
                SharedAccessNormalizedEmail: null,
                LimitToSharedAccess: false,
                SharedWithMe: false,
                TaskItemSortField.LastTouchedAt,
                SortDescending: true,
                now),
            cancellationToken);
    }

    private async Task PushLocalTaskAsync(
        CloudSyncConnection connection,
        Guid remoteWorkspaceId,
        SyncRoot root,
        TaskItem localTask,
        Dictionary<Guid, SyncMapping> mappings,
        Dictionary<Guid, CloudSyncTaskResponse> remoteTasks,
        SyncWorkspaceStats stats,
        List<string> messages,
        CancellationToken cancellationToken)
    {
        var mapping = await EnsureMappingForLocalTaskAsync(root, localTask, mappings, cancellationToken);

        if (localTask.ArchivedAt.HasValue)
        {
            messages.Add($"Skipped archived local task \"{localTask.Title}\". Archive sync is not implemented yet.");
            return;
        }

        try
        {
            if (!mapping.RemoteId.HasValue)
            {
                var createdRemoteTask = await _cloudSyncClient.CreateTaskAsync(
                    connection,
                    remoteWorkspaceId,
                    new CloudSyncCreateTaskRequest(
                        localTask.Title,
                        localTask.Status,
                        localTask.Category,
                        localTask.Color,
                        localTask.FollowUpAt),
                    cancellationToken);

                mapping.LinkRemote(
                    createdRemoteTask.Id,
                    CreateRemoteVersion(createdRemoteTask),
                    _clock.UtcNow);
                remoteTasks[createdRemoteTask.Id] = createdRemoteTask;
                stats.Pushed++;
                return;
            }

            if (!remoteTasks.TryGetValue(mapping.RemoteId.Value, out var remoteTask))
            {
                mapping.MarkSyncFailed("Mapped cloud task was not found.", _clock.UtcNow);
                stats.Failed++;
                return;
            }

            var lastSyncedAt = mapping.LastSyncedAt;
            var localChanged = !lastSyncedAt.HasValue || localTask.LastTouchedAt > lastSyncedAt.Value;
            var remoteChanged = !lastSyncedAt.HasValue || remoteTask.LastTouchedAt > lastSyncedAt.Value;

            if (localChanged && remoteChanged && HeaderDiffers(localTask, remoteTask))
            {
                mapping.MarkConflict(_clock.UtcNow);
                stats.Conflicts++;
                messages.Add($"Conflict kept for \"{localTask.Title}\". Both local and cloud changed since last sync.");
                return;
            }

            if (localChanged && HeaderDiffers(localTask, remoteTask))
            {
                var updatedRemoteTask = await _cloudSyncClient.UpdateTaskAsync(
                    connection,
                    remoteWorkspaceId,
                    remoteTask.Id,
                    new CloudSyncUpdateTaskRequest(
                        localTask.Title,
                        localTask.Status,
                        localTask.Category,
                        localTask.Color,
                        localTask.FollowUpAt),
                    cancellationToken);

                mapping.LinkRemote(
                    updatedRemoteTask.Id,
                    CreateRemoteVersion(updatedRemoteTask),
                    _clock.UtcNow);
                remoteTasks[updatedRemoteTask.Id] = updatedRemoteTask;
                stats.UpdatedRemote++;
                return;
            }

            if (remoteChanged && ApplyRemoteHeaderToLocal(localTask, remoteTask, _clock.UtcNow))
            {
                stats.UpdatedLocal++;
            }

            mapping.LinkRemote(
                remoteTask.Id,
                CreateRemoteVersion(remoteTask),
                _clock.UtcNow);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            mapping.MarkSyncFailed(exception.Message, _clock.UtcNow);
            stats.Failed++;
        }
    }

    private async Task PullNewRemoteTaskAsync(
        SyncRoot root,
        Guid localWorkspaceId,
        CloudSyncTaskResponse remoteTask,
        Dictionary<Guid, SyncMapping> mappings,
        SyncWorkspaceStats stats,
        List<string> messages,
        CancellationToken cancellationToken)
    {
        if (remoteTask.ArchivedAt.HasValue)
        {
            messages.Add($"Skipped archived cloud task \"{remoteTask.Title}\". Archive sync is not implemented yet.");
            return;
        }

        var localTask = TaskItem.Create(
            localWorkspaceId,
            projectId: null,
            remoteTask.Title,
            _clock.UtcNow,
            taskTemplateId: null);
        ApplyRemoteHeaderToLocal(localTask, remoteTask, _clock.UtcNow);

        await _taskItemRepository.AddAsync(localTask, cancellationToken);
        var mapping = SyncMapping.CreateLocal(
            root.Id,
            SyncEntityType.TaskItem,
            localTask.Id,
            _clock.UtcNow);
        mapping.LinkRemote(
            remoteTask.Id,
            CreateRemoteVersion(remoteTask),
            _clock.UtcNow);
        await _syncRepository.AddMappingAsync(mapping, cancellationToken);
        mappings[localTask.Id] = mapping;
        stats.Pulled++;
    }

    private async Task<SyncMapping> EnsureMappingForLocalTaskAsync(
        SyncRoot root,
        TaskItem localTask,
        Dictionary<Guid, SyncMapping> mappings,
        CancellationToken cancellationToken)
    {
        if (mappings.TryGetValue(localTask.Id, out var existingMapping))
        {
            return existingMapping;
        }

        var mapping = SyncMapping.CreateLocal(
            root.Id,
            SyncEntityType.TaskItem,
            localTask.Id,
            _clock.UtcNow);
        await _syncRepository.AddMappingAsync(mapping, cancellationToken);
        mappings[localTask.Id] = mapping;

        return mapping;
    }

    private static bool HeaderDiffers(TaskItem localTask, CloudSyncTaskResponse remoteTask)
    {
        return !string.Equals(localTask.Title, remoteTask.Title, StringComparison.Ordinal) ||
            !string.Equals(localTask.Status, remoteTask.Status, StringComparison.Ordinal) ||
            !string.Equals(localTask.Category, remoteTask.Category, StringComparison.Ordinal) ||
            !string.Equals(localTask.Color, remoteTask.Color, StringComparison.OrdinalIgnoreCase) ||
            localTask.FollowUpAt != remoteTask.FollowUpAt;
    }

    private static bool ApplyRemoteHeaderToLocal(
        TaskItem localTask,
        CloudSyncTaskResponse remoteTask,
        DateTimeOffset occurredAt)
    {
        var originalTouchedAt = localTask.LastTouchedAt;

        localTask.Rename(remoteTask.Title, occurredAt);
        localTask.ChangeStatus(remoteTask.Status, occurredAt);
        localTask.ChangeCategory(remoteTask.Category, occurredAt);
        localTask.ChangeColor(remoteTask.Color, occurredAt);
        localTask.SetFollowUp(remoteTask.FollowUpAt, occurredAt);

        return localTask.LastTouchedAt != originalTouchedAt;
    }

    private static string CreateRemoteVersion(CloudSyncTaskResponse remoteTask)
    {
        return remoteTask.LastTouchedAt.ToString("O");
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

    private sealed class SyncWorkspaceStats
    {
        public int Pushed { get; set; }

        public int Pulled { get; set; }

        public int UpdatedLocal { get; set; }

        public int UpdatedRemote { get; set; }

        public int Conflicts { get; set; }

        public int Failed { get; set; }
    }
}
