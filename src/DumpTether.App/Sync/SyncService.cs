using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using DumpTether.App.Auth;
using DumpTether.App.Tasks;
using DumpTether.App.Templates;
using DumpTether.App.Workspaces;
using DumpTether.Domain;

namespace DumpTether.App.Sync;

internal sealed class SyncService : ISyncService
{
    private const string DefaultLocalDesktopDeviceId = "local-desktop";

    private readonly IAuthRepository _authRepository;
    private readonly ICloudSessionProtector _cloudSessionProtector;
    private readonly ICloudSyncClient _cloudSyncClient;
    private readonly IClock _clock;
    private readonly ICurrentUserSessionProvider _currentUserSessionProvider;
    private readonly ISyncRepository _syncRepository;
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly ITaskTemplateRepository _taskTemplateRepository;
    private readonly IWorkspaceRepository _workspaceRepository;

    public SyncService(
        IAuthRepository authRepository,
        ICloudSessionProtector cloudSessionProtector,
        ICloudSyncClient cloudSyncClient,
        IClock clock,
        ICurrentUserSessionProvider currentUserSessionProvider,
        ISyncRepository syncRepository,
        ITaskItemRepository taskItemRepository,
        ITaskTemplateRepository taskTemplateRepository,
        IWorkspaceRepository workspaceRepository)
    {
        _authRepository = authRepository;
        _cloudSessionProtector = cloudSessionProtector;
        _cloudSyncClient = cloudSyncClient;
        _clock = clock;
        _currentUserSessionProvider = currentUserSessionProvider;
        _syncRepository = syncRepository;
        _taskItemRepository = taskItemRepository;
        _taskTemplateRepository = taskTemplateRepository;
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

    public async Task<CloudSyncAccountResponse?> GetCloudAccountAsync(
        CancellationToken cancellationToken)
    {
        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        if (!IsDesktopSession(currentSession))
        {
            throw new UnauthorizedAccessException("Desktop sync session is required.");
        }

        var account = await _syncRepository.GetCloudAccountForUserAsync(
            currentSession.UserId,
            trackChanges: false,
            cancellationToken);

        return account is null ? null : MapCloudAccount(account, _clock.UtcNow);
    }

    public async Task<CloudSyncAccountResponse> ConnectCloudAccountAsync(
        ConnectCloudAccountRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        if (!IsDesktopSession(currentSession))
        {
            throw new UnauthorizedAccessException("Desktop sync session is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ValidationException("Cloud email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException("Cloud password is required.");
        }

        var normalizedCloudApiBaseUrl = CloudSyncAccount.NormalizeCloudApiBaseUrl(request.CloudApiBaseUrl);
        var login = await _cloudSyncClient.LoginAsync(
            normalizedCloudApiBaseUrl,
            new CloudSyncLoginRequest(
                request.Email,
                request.Password,
                request.DeviceName),
            cancellationToken);
        var now = _clock.UtcNow;
        var protectedToken = _cloudSessionProtector.Protect(login.SessionToken);
        var account = await _syncRepository.GetCloudAccountForUserAsync(
            currentSession.UserId,
            trackChanges: true,
            cancellationToken);

        if (account is null)
        {
            account = CloudSyncAccount.Create(
                currentSession.UserId,
                normalizedCloudApiBaseUrl,
                login.User.Id,
                login.User.Email,
                login.User.DisplayName,
                protectedToken,
                login.ExpiresAt,
                now);
            await _syncRepository.AddCloudAccountAsync(account, cancellationToken);
        }
        else
        {
            account.ReplaceConnection(
                normalizedCloudApiBaseUrl,
                login.User.Id,
                login.User.Email,
                login.User.DisplayName,
                protectedToken,
                login.ExpiresAt,
                now);
        }

        await _syncRepository.SaveChangesAsync(cancellationToken);

        return MapCloudAccount(account, now);
    }

    public async Task<DisconnectCloudAccountResponse> DisconnectCloudAccountAsync(
        CancellationToken cancellationToken)
    {
        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        if (!IsDesktopSession(currentSession))
        {
            throw new UnauthorizedAccessException("Desktop sync session is required.");
        }

        var account = await _syncRepository.GetCloudAccountForUserAsync(
            currentSession.UserId,
            trackChanges: true,
            cancellationToken);

        if (account is null || account.DisconnectedAt.HasValue)
        {
            return new DisconnectCloudAccountResponse(false);
        }

        try
        {
            var sessionToken = _cloudSessionProtector.Unprotect(account.ProtectedSessionToken);
            await _cloudSyncClient.LogoutAsync(
                new CloudSyncConnection(account.CloudApiBaseUrl, sessionToken),
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Disconnect is local-first. The remote session still expires server-side
            // if the cloud endpoint cannot be reached for best-effort revocation.
        }

        account.Disconnect(_clock.UtcNow);
        await _syncRepository.SaveChangesAsync(cancellationToken);

        return new DisconnectCloudAccountResponse(true);
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
        var preparedConnection = await CreateCloudConnectionAsync(
            request,
            currentSession.UserId,
            cancellationToken);
        var connection = preparedConnection.Connection;
        var cloudUser = await _cloudSyncClient.GetCurrentUserAsync(connection, cancellationToken);
        var now = _clock.UtcNow;
        preparedConnection.Account?.MarkVerified(
            cloudUser.Id,
            cloudUser.Email,
            cloudUser.DisplayName,
            now);
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
            if (!request.PushLocalChanges)
            {
                foreach (var localTask in localTasks)
                {
                    if (!mappings.TryGetValue(localTask.Id, out var mapping) ||
                        mapping.RemoteId is not Guid remoteTaskId ||
                        !remoteTasks.TryGetValue(remoteTaskId, out var remoteTask))
                    {
                        continue;
                    }

                    await PullMappedRemoteTaskAsync(
                        connection,
                        root,
                        localTask,
                        mapping,
                        remoteTask,
                        stats,
                        messages,
                        cancellationToken);
                }

                remoteIdsAlreadyMapped = mappings.Values
                    .Where(mapping => mapping.RemoteId.HasValue)
                    .Select(mapping => mapping.RemoteId!.Value)
                    .ToHashSet();
            }

            foreach (var remoteTask in remoteTasks.Values)
            {
                if (remoteIdsAlreadyMapped.Contains(remoteTask.Id))
                {
                    continue;
                }

                await PullNewRemoteTaskAsync(
                    connection,
                    root,
                    workspaceId,
                    currentSession.UserId,
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
        else if (stats.Failed == 0 && root.RemoteWorkspaceId.HasValue)
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

    private async Task<PreparedCloudConnection> CreateCloudConnectionAsync(
        SyncWorkspaceWithCloudRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var hasManualCloudApiBaseUrl = !string.IsNullOrWhiteSpace(request.CloudApiBaseUrl);
        var hasManualSessionToken = !string.IsNullOrWhiteSpace(request.CloudSessionToken);

        if (hasManualCloudApiBaseUrl || hasManualSessionToken)
        {
            return new PreparedCloudConnection(
                CreateManualCloudConnection(request),
                Account: null);
        }

        var account = await _syncRepository.GetCloudAccountForUserAsync(
            userId,
            trackChanges: true,
            cancellationToken);
        var now = _clock.UtcNow;

        if (account is null || account.DisconnectedAt.HasValue)
        {
            throw new ValidationException("Connect a cloud account before syncing this board.");
        }

        if (!account.HasUsableSession(now))
        {
            throw new ValidationException("Cloud account session expired. Reconnect the cloud account before syncing.");
        }

        string sessionToken;
        try
        {
            sessionToken = _cloudSessionProtector.Unprotect(account.ProtectedSessionToken);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Cloud account session could not be read. Reconnect the cloud account before syncing.",
                exception);
        }

        return new PreparedCloudConnection(
            new CloudSyncConnection(
                account.CloudApiBaseUrl,
                sessionToken),
            account);
    }

    private static CloudSyncConnection CreateManualCloudConnection(SyncWorkspaceWithCloudRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CloudApiBaseUrl))
        {
            throw new ArgumentException("Cloud API base URL is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.CloudSessionToken))
        {
            throw new ArgumentException("Cloud session token is required.", nameof(request));
        }

        if (!Uri.TryCreate(request.CloudApiBaseUrl.Trim(), UriKind.Absolute, out var cloudApiBaseUrl) ||
            (cloudApiBaseUrl.Scheme != Uri.UriSchemeHttps && cloudApiBaseUrl.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrWhiteSpace(cloudApiBaseUrl.UserInfo))
        {
            throw new ArgumentException(
                "Cloud API base URL must be an absolute HTTP(S) URL without credentials.",
                nameof(request));
        }

        return new CloudSyncConnection(
            cloudApiBaseUrl.AbsoluteUri.TrimEnd('/'),
            request.CloudSessionToken.Trim());
    }

    private async Task<CloudSyncWorkspaceResponse> ResolveRemoteWorkspaceAsync(
        CloudSyncConnection connection,
        Workspace localWorkspace,
        SyncRoot root,
        Guid? requestedRemoteWorkspaceId,
        CancellationToken cancellationToken)
    {
        var remoteWorkspaces = await _cloudSyncClient.ListWorkspacesAsync(
            connection,
            cancellationToken);
        var remoteWorkspaceId = requestedRemoteWorkspaceId ?? root.RemoteWorkspaceId;

        if (remoteWorkspaceId.HasValue)
        {
            var existingRemoteWorkspace = remoteWorkspaces
                .FirstOrDefault(workspace => workspace.Id == remoteWorkspaceId.Value);

            return existingRemoteWorkspace ??
                throw new ValidationException("Selected cloud board was not found for this cloud user.");
        }

        var matchingRemoteWorkspace = remoteWorkspaces.FirstOrDefault(workspace =>
            string.Equals(
                workspace.Name,
                localWorkspace.Name,
                StringComparison.OrdinalIgnoreCase));

        if (matchingRemoteWorkspace is not null)
        {
            return matchingRemoteWorkspace;
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
                var remoteTemplate = await EnsureRemoteTaskTemplateAsync(
                    connection,
                    root,
                    localTask,
                    messages,
                    cancellationToken);
                var remoteFieldValues = CloudSyncTemplatePayloadMapper.BuildRemoteFieldValuePayload(
                    localTask,
                    remoteTemplate.LocalToRemoteFieldIds);
                var createdRemoteTask = await _cloudSyncClient.CreateTaskAsync(
                    connection,
                    remoteWorkspaceId,
                    new CloudSyncCreateTaskRequest(
                        localTask.Title,
                        remoteTemplate.RemoteTemplateId,
                        localTask.Status,
                        localTask.Category,
                        localTask.Color,
                        localTask.FollowUpAt,
                        remoteFieldValues,
                        BuildRemoteTimelineEntryPayload(localTask, remoteTemplate.LocalToRemoteFieldIds)),
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
            var templateProjection = await EnsureRemoteTaskTemplateAsync(
                connection,
                root,
                localTask,
                messages,
                cancellationToken);
            var remoteFieldPayload = CloudSyncTemplatePayloadMapper.BuildRemoteFieldValuePayload(
                localTask,
                templateProjection.LocalToRemoteFieldIds);
            var payloadDiffers = TaskPayloadDiffers(
                localTask,
                remoteTask,
                templateProjection.RemoteTemplateId,
                remoteFieldPayload);

            if (localChanged && remoteChanged && payloadDiffers)
            {
                mapping.MarkConflict(_clock.UtcNow);
                stats.Conflicts++;
                messages.Add($"Conflict kept for \"{localTask.Title}\". Both local and cloud changed since last sync.");
                return;
            }

            if (localChanged && payloadDiffers)
            {
                var updatedRemoteTask = await _cloudSyncClient.UpdateTaskAsync(
                    connection,
                    remoteWorkspaceId,
                    remoteTask.Id,
                    new CloudSyncUpdateTaskRequest(
                        localTask.Title,
                        templateProjection.RemoteTemplateId,
                        localTask.Status,
                        localTask.Category,
                        localTask.Color,
                        localTask.FollowUpAt,
                        remoteFieldPayload),
                    cancellationToken);

                mapping.LinkRemote(
                    updatedRemoteTask.Id,
                    CreateRemoteVersion(updatedRemoteTask),
                    _clock.UtcNow);
                remoteTasks[updatedRemoteTask.Id] = updatedRemoteTask;
                stats.UpdatedRemote++;
                return;
            }

            if (remoteChanged)
            {
                var trackedLocalTask = await GetTrackedLocalTaskForSyncAsync(localTask, cancellationToken);
                var changed = ApplyRemoteHeaderToLocal(trackedLocalTask, remoteTask, _clock.UtcNow);
                changed |= ApplyRemoteFieldValuesToMappedLocal(
                    trackedLocalTask,
                    templateProjection,
                    remoteTask,
                    _clock.UtcNow);

                if (changed)
                {
                    stats.UpdatedLocal++;
                }
            }

            mapping.LinkRemote(
                remoteTask.Id,
                CreateRemoteVersion(remoteTask),
                _clock.UtcNow);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or ValidationException)
        {
            mapping.MarkSyncFailed(exception.Message, _clock.UtcNow);
            stats.Failed++;
        }
    }

    private async Task PullMappedRemoteTaskAsync(
        CloudSyncConnection connection,
        SyncRoot root,
        TaskItem localTask,
        SyncMapping mapping,
        CloudSyncTaskResponse remoteTask,
        SyncWorkspaceStats stats,
        List<string> messages,
        CancellationToken cancellationToken)
    {
        if (remoteTask.ArchivedAt.HasValue)
        {
            messages.Add($"Skipped archived cloud task \"{remoteTask.Title}\". Archive sync is not implemented yet.");
            return;
        }

        try
        {
            var trackedLocalTask = await GetTrackedLocalTaskForSyncAsync(localTask, cancellationToken);
            var lastSyncedAt = mapping.LastSyncedAt;
            var localChanged = !lastSyncedAt.HasValue || trackedLocalTask.LastTouchedAt > lastSyncedAt.Value;
            var remoteChanged = !lastSyncedAt.HasValue || remoteTask.LastTouchedAt > lastSyncedAt.Value;

            if (!remoteChanged)
            {
                return;
            }

            if (localChanged)
            {
                mapping.MarkConflict(_clock.UtcNow);
                stats.Conflicts++;
                messages.Add($"Conflict kept for \"{trackedLocalTask.Title}\". Local and cloud changed since last sync.");
                return;
            }

            var templateProjection = await ResolveMappedRemoteTaskTemplateProjectionAsync(
                connection,
                trackedLocalTask,
                remoteTask,
                cancellationToken);
            var changed = ApplyRemoteHeaderToLocal(trackedLocalTask, remoteTask, _clock.UtcNow);
            changed |= ApplyRemoteFieldValuesToMappedLocal(
                trackedLocalTask,
                templateProjection,
                remoteTask,
                _clock.UtcNow);

            if (changed)
            {
                stats.UpdatedLocal++;
            }

            mapping.LinkRemote(
                remoteTask.Id,
                CreateRemoteVersion(remoteTask),
                _clock.UtcNow);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or ValidationException)
        {
            mapping.MarkSyncFailed(exception.Message, _clock.UtcNow);
            stats.Failed++;
        }
    }

    private async Task PullNewRemoteTaskAsync(
        CloudSyncConnection connection,
        SyncRoot root,
        Guid localWorkspaceId,
        Guid ownerUserId,
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

        var localTemplate = await EnsureLocalTaskTemplateAsync(
            connection,
            root,
            ownerUserId,
            remoteTask,
            messages,
            cancellationToken);
        var localTask = TaskItem.Create(
            localWorkspaceId,
            projectId: null,
            remoteTask.Title,
            _clock.UtcNow,
            localTemplate.LocalTemplateId);
        ApplyRemoteHeaderToLocal(localTask, remoteTask, _clock.UtcNow);
        ApplyRemoteFieldValuesToLocal(
            localTask,
            localTemplate,
            remoteTask,
            _clock.UtcNow);
        ApplyRemoteTimelineEntriesToLocal(
            localTask,
            localTemplate,
            remoteTask);

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

    private static IReadOnlyList<CloudSyncTimelineEntryRequest>? BuildRemoteTimelineEntryPayload(
        TaskItem localTask,
        IReadOnlyDictionary<Guid, Guid> localToRemoteFieldIds)
    {
        var entries = localTask.TimelineEntries
            .Where(entry =>
                entry.Kind == TaskTimelineEntryKind.NoteAdded &&
                entry.DeletedAt is null &&
                (!string.IsNullOrWhiteSpace(entry.Details) || entry.FieldValues.Count > 0))
            .OrderBy(entry => entry.OccurredAt)
            .Select(entry => new CloudSyncTimelineEntryRequest(
                string.IsNullOrWhiteSpace(entry.Details) ? null : entry.Details,
                CloudSyncTemplatePayloadMapper.BuildRemoteFieldValuePayload(
                    entry,
                    localToRemoteFieldIds)))
            .ToList();

        return entries.Count == 0 ? null : entries;
    }

    private static bool ApplyRemoteTimelineEntriesToLocal(
        TaskItem localTask,
        RemoteToLocalTaskTemplateProjection localTemplate,
        CloudSyncTaskResponse remoteTask)
    {
        if (localTemplate.LocalTemplate is null ||
            remoteTask.TimelineEntries is null ||
            remoteTask.TimelineEntries.Count == 0)
        {
            return false;
        }

        var definitions = localTemplate.LocalTemplate.FieldDefinitions
            .Where(field => field.IsActive && field.Scope == FieldDefinitionScope.Entry)
            .ToDictionary(field => field.Id);
        var changed = false;

        foreach (var remoteEntry in remoteTask.TimelineEntries
                     .OrderBy(entry => entry.OccurredAt))
        {
            var localEntry = localTask.AddNote(
                string.IsNullOrWhiteSpace(remoteEntry.Details) ? null : remoteEntry.Details,
                remoteEntry.OccurredAt);
            changed = true;

            var fieldValues = CloudSyncTemplatePayloadMapper.BuildLocalFieldValuePayload(
                remoteEntry,
                localTemplate.RemoteToLocalFieldIds);

            if (fieldValues is null)
            {
                continue;
            }

            foreach (var (fieldDefinitionId, valueJson) in fieldValues)
            {
                if (!definitions.TryGetValue(fieldDefinitionId, out var definition))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(valueJson);
                localTask.SetTimelineEntryFieldValue(
                    localEntry.Id,
                    definition,
                    document.RootElement.GetRawText(),
                    remoteEntry.OccurredAt);
            }
        }

        return changed;
    }

    private async Task<RemoteToLocalTaskTemplateProjection> EnsureLocalTaskTemplateAsync(
        CloudSyncConnection connection,
        SyncRoot root,
        Guid ownerUserId,
        CloudSyncTaskResponse remoteTask,
        List<string> messages,
        CancellationToken cancellationToken)
    {
        if (!remoteTask.TaskTemplateId.HasValue)
        {
            return RemoteToLocalTaskTemplateProjection.Empty;
        }

        var remoteTemplate = await _cloudSyncClient.GetTaskTemplateAsync(
                connection,
                remoteTask.TaskTemplateId.Value,
                cancellationToken) ??
            throw new ValidationException("Cloud task template was not found.");
        var templateMappings = await _syncRepository.ListMappingsForRootAsync(
            root.Id,
            SyncEntityType.TaskTemplate,
            trackChanges: true,
            cancellationToken);
        var mapping = templateMappings.FirstOrDefault(candidate =>
            candidate.RemoteId == remoteTemplate.Id);

        if (mapping is not null)
        {
            var existingLocalTemplate = await _taskTemplateRepository.GetByIdAsync(
                    mapping.LocalId,
                    ownerUserId,
                    trackChanges: true,
                    includeDeleted: true,
                    cancellationToken) ??
                throw new ValidationException("Mapped local task template was not found.");

            return CloudSyncTemplatePayloadMapper.CreateRemoteToLocalProjection(
                remoteTemplate,
                existingLocalTemplate);
        }

        var localTemplate = CloudSyncTemplatePayloadMapper.CreateLocalTemplate(
            remoteTemplate,
            ownerUserId,
            _clock.UtcNow);
        await _taskTemplateRepository.AddAsync(localTemplate, cancellationToken);
        mapping = SyncMapping.CreateLocal(
            root.Id,
            SyncEntityType.TaskTemplate,
            localTemplate.Id,
            _clock.UtcNow);
        mapping.LinkRemote(
            remoteTemplate.Id,
            CreateRemoteVersion(remoteTemplate),
            _clock.UtcNow);
        await _syncRepository.AddMappingAsync(mapping, cancellationToken);
        messages.Add($"Imported cloud template \"{remoteTemplate.Name}\".");

        return CloudSyncTemplatePayloadMapper.CreateRemoteToLocalProjection(
            remoteTemplate,
            localTemplate);
    }

    private static bool ApplyRemoteFieldValuesToLocal(
        TaskItem localTask,
        RemoteToLocalTaskTemplateProjection localTemplate,
        CloudSyncTaskResponse remoteTask,
        DateTimeOffset occurredAt)
    {
        if (localTemplate.LocalTemplate is null)
        {
            return false;
        }

        var fieldValues = CloudSyncTemplatePayloadMapper.BuildLocalFieldValuePayload(
            remoteTask,
            localTemplate.RemoteToLocalFieldIds);

        if (fieldValues is null || fieldValues.Count == 0)
        {
            return false;
        }

        var definitions = localTemplate.LocalTemplate.FieldDefinitions
            .Where(field => field.IsActive && field.Scope == FieldDefinitionScope.Header)
            .ToDictionary(field => field.Id);
        var changed = false;

        foreach (var (fieldDefinitionId, valueJson) in fieldValues)
        {
            if (!definitions.TryGetValue(fieldDefinitionId, out var definition))
            {
                continue;
            }

            changed |= localTask.SetFieldValue(definition, valueJson, occurredAt);
        }

        return changed;
    }

    private static bool ApplyRemoteFieldValuesToMappedLocal(
        TaskItem localTask,
        RemoteTaskTemplateProjection templateProjection,
        CloudSyncTaskResponse remoteTask,
        DateTimeOffset occurredAt)
    {
        if (templateProjection.LocalTemplate is null)
        {
            return false;
        }

        var remoteToLocalHeaderFieldIds = templateProjection.LocalToRemoteFieldIds
            .ToDictionary(pair => pair.Value, pair => pair.Key);
        var fieldValues = CloudSyncTemplatePayloadMapper.BuildLocalFieldValuePayload(
            remoteTask,
            remoteToLocalHeaderFieldIds);

        if (fieldValues is null || fieldValues.Count == 0)
        {
            return false;
        }

        var definitions = templateProjection.LocalTemplate.FieldDefinitions
            .Where(field => field.IsActive && field.Scope == FieldDefinitionScope.Header)
            .ToDictionary(field => field.Id);
        var changed = false;

        foreach (var (fieldDefinitionId, valueJson) in fieldValues)
        {
            if (!definitions.TryGetValue(fieldDefinitionId, out var definition))
            {
                continue;
            }

            changed |= localTask.SetFieldValue(definition, valueJson, occurredAt);
        }

        return changed;
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

    private async Task<TaskItem> GetTrackedLocalTaskForSyncAsync(
        TaskItem localTask,
        CancellationToken cancellationToken)
    {
        return await _taskItemRepository.GetByIdAsync(
                localTask.Id,
                localTask.WorkspaceId,
                projectId: null,
                trackChanges: true,
                cancellationToken) ??
            throw new ValidationException("Local task was not found.");
    }

    private static bool HeaderDiffers(TaskItem localTask, CloudSyncTaskResponse remoteTask)
    {
        return !string.Equals(localTask.Title, remoteTask.Title, StringComparison.Ordinal) ||
            !string.Equals(localTask.Status, remoteTask.Status, StringComparison.Ordinal) ||
            !string.Equals(localTask.Category, remoteTask.Category, StringComparison.Ordinal) ||
            !string.Equals(localTask.Color, remoteTask.Color, StringComparison.OrdinalIgnoreCase) ||
            localTask.FollowUpAt != remoteTask.FollowUpAt;
    }

    private async Task<RemoteTaskTemplateProjection> EnsureRemoteTaskTemplateAsync(
        CloudSyncConnection connection,
        SyncRoot root,
        TaskItem localTask,
        List<string> messages,
        CancellationToken cancellationToken)
    {
        if (!localTask.TaskTemplateId.HasValue)
        {
            return RemoteTaskTemplateProjection.Empty;
        }

        var localTemplate = await _taskItemRepository.GetTaskTemplateByIdAsync(
                localTask.TaskTemplateId.Value,
                includeDeleted: true,
                cancellationToken) ??
            throw new ValidationException("Local task template was not found.");
        var mapping = await _syncRepository.GetMappingAsync(
            root.Id,
            SyncEntityType.TaskTemplate,
            localTemplate.Id,
            trackChanges: true,
            cancellationToken);
        CloudSyncTaskTemplateResponse? remoteTemplate = null;

        if (mapping?.RemoteId is Guid remoteTemplateId)
        {
            remoteTemplate = await _cloudSyncClient.GetTaskTemplateAsync(
                connection,
                remoteTemplateId,
                cancellationToken);
        }

        if (remoteTemplate is null)
        {
            if (mapping?.RemoteId is not null)
            {
                throw new InvalidOperationException(
                    $"Mapped cloud template \"{localTemplate.Name}\" was not found.");
            }

            if (mapping is null)
            {
                mapping = SyncMapping.CreateLocal(
                    root.Id,
                    SyncEntityType.TaskTemplate,
                    localTemplate.Id,
                    _clock.UtcNow);

                await _syncRepository.AddMappingAsync(mapping, cancellationToken);
            }

            var matchingRemoteTemplate = (await _cloudSyncClient.ListTaskTemplatesAsync(
                    connection,
                    cancellationToken))
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Name,
                    localTemplate.Name,
                    StringComparison.OrdinalIgnoreCase));

            if (matchingRemoteTemplate is not null)
            {
                if (CloudSyncTemplatePayloadMapper.TemplateDiffers(
                        localTemplate,
                        matchingRemoteTemplate))
                {
                    throw new InvalidOperationException(
                        $"Cloud template \"{localTemplate.Name}\" has a different structure. " +
                        "Rename one template before syncing this task.");
                }

                mapping.LinkRemote(
                    matchingRemoteTemplate.Id,
                    CreateRemoteVersion(matchingRemoteTemplate),
                    _clock.UtcNow);
                messages.Add($"Linked existing cloud template \"{localTemplate.Name}\".");

                return CloudSyncTemplatePayloadMapper.CreateProjection(
                    matchingRemoteTemplate,
                    localTemplate);
            }

            remoteTemplate = await _cloudSyncClient.CreateTaskTemplateAsync(
                connection,
                CloudSyncTemplatePayloadMapper.CreateRequest(localTemplate),
                cancellationToken);
            mapping.LinkRemote(
                remoteTemplate.Id,
                CreateRemoteVersion(remoteTemplate),
                _clock.UtcNow);
            messages.Add($"Created cloud template \"{localTemplate.Name}\".");

            return CloudSyncTemplatePayloadMapper.CreateProjection(remoteTemplate, localTemplate);
        }

        var lastSyncedAt = mapping?.LastSyncedAt;
        var localChanged = !lastSyncedAt.HasValue || localTemplate.UpdatedAt > lastSyncedAt.Value;
        var remoteChanged = !lastSyncedAt.HasValue || remoteTemplate.UpdatedAt > lastSyncedAt.Value;

        if (localChanged &&
            remoteChanged &&
            CloudSyncTemplatePayloadMapper.TemplateDiffers(localTemplate, remoteTemplate))
        {
            throw new InvalidOperationException(
                $"Template \"{localTemplate.Name}\" changed locally and in the cloud. Resolve template differences before syncing this task.");
        }

        if (localChanged && CloudSyncTemplatePayloadMapper.TemplateDiffers(localTemplate, remoteTemplate))
        {
            remoteTemplate = await _cloudSyncClient.UpdateTaskTemplateAsync(
                connection,
                remoteTemplate.Id,
                CloudSyncTemplatePayloadMapper.UpdateRequest(localTemplate, remoteTemplate),
                cancellationToken);
            mapping!.LinkRemote(
                remoteTemplate.Id,
                CreateRemoteVersion(remoteTemplate),
                _clock.UtcNow);
            messages.Add($"Updated cloud template \"{localTemplate.Name}\".");
        }

        return CloudSyncTemplatePayloadMapper.CreateProjection(remoteTemplate, localTemplate);
    }

    private async Task<RemoteTaskTemplateProjection> ResolveMappedRemoteTaskTemplateProjectionAsync(
        CloudSyncConnection connection,
        TaskItem localTask,
        CloudSyncTaskResponse remoteTask,
        CancellationToken cancellationToken)
    {
        if (!localTask.TaskTemplateId.HasValue || !remoteTask.TaskTemplateId.HasValue)
        {
            return RemoteTaskTemplateProjection.Empty;
        }

        var localTemplate = await _taskItemRepository.GetTaskTemplateByIdAsync(
                localTask.TaskTemplateId.Value,
                includeDeleted: true,
                cancellationToken) ??
            throw new ValidationException("Local task template was not found.");
        var remoteTemplate = await _cloudSyncClient.GetTaskTemplateAsync(
                connection,
                remoteTask.TaskTemplateId.Value,
                cancellationToken) ??
            throw new ValidationException("Cloud task template was not found.");

        return CloudSyncTemplatePayloadMapper.CreateProjection(remoteTemplate, localTemplate);
    }

    private static bool TaskPayloadDiffers(
        TaskItem localTask,
        CloudSyncTaskResponse remoteTask,
        Guid? remoteTemplateId,
        IReadOnlyDictionary<Guid, string>? remoteFieldPayload)
    {
        if (HeaderDiffers(localTask, remoteTask) ||
            remoteTask.TaskTemplateId != remoteTemplateId)
        {
            return true;
        }

        return RemoteFieldValuesDiffer(remoteTask, remoteFieldPayload);
    }

    private static bool RemoteFieldValuesDiffer(
        CloudSyncTaskResponse remoteTask,
        IReadOnlyDictionary<Guid, string>? remoteFieldPayload)
    {
        if (remoteFieldPayload is null || remoteFieldPayload.Count == 0)
        {
            return false;
        }

        var remoteValues = (remoteTask.FieldValues ?? [])
            .ToDictionary(value => value.FieldDefinitionId);

        foreach (var (fieldDefinitionId, valueJson) in remoteFieldPayload)
        {
            if (!remoteValues.TryGetValue(fieldDefinitionId, out var remoteValue) ||
                !string.Equals(remoteValue.ValueJson, valueJson, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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

    private static string CreateRemoteVersion(CloudSyncTaskTemplateResponse remoteTemplate)
    {
        return remoteTemplate.UpdatedAt.ToString("O");
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

    private static CloudSyncAccountResponse MapCloudAccount(
        CloudSyncAccount account,
        DateTimeOffset now)
    {
        return new CloudSyncAccountResponse(
            account.Id,
            account.CloudApiBaseUrl,
            account.CloudUserId,
            account.CloudEmail,
            account.CloudDisplayName,
            account.SessionExpiresAt,
            account.ConnectedAt,
            account.UpdatedAt,
            account.LastVerifiedAt,
            account.HasUsableSession(now));
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

    private sealed record PreparedCloudConnection(
        CloudSyncConnection Connection,
        CloudSyncAccount? Account);
}
