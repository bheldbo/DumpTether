using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using DumpTether.App.Auth;
using DumpTether.App.LiveUpdates;
using DumpTether.App.Projects;
using DumpTether.App.Sync;
using DumpTether.App.Templates;
using DumpTether.App.Usage;
using DumpTether.App.Views;
using DumpTether.Domain;
using Microsoft.Extensions.Options;

namespace DumpTether.App.Tasks;

internal sealed class TaskItemService : ITaskItemService
{
    private static readonly TimeSpan ShareLinkLifetime = TimeSpan.FromDays(1);

    private readonly IClock _clock;
    private readonly IAuthRepository _authRepository;
    private readonly ICurrentUserSessionProvider _currentUserSessionProvider;
    private readonly IDevelopmentWorkspaceProvider _developmentWorkspaceProvider;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;
    private readonly IProjectRepository _projectRepository;
    private readonly ISavedViewRepository _savedViewRepository;
    private readonly ISessionTokenService _sessionTokenService;
    private readonly ISyncService _syncService;
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly ITaskTemplateRepository _taskTemplateRepository;
    private readonly IOptions<UsageOptions> _usageOptions;

    public TaskItemService(
        IClock clock,
        IAuthRepository authRepository,
        ICurrentUserSessionProvider currentUserSessionProvider,
        IDevelopmentWorkspaceProvider developmentWorkspaceProvider,
        ILiveUpdatePublisher liveUpdatePublisher,
        IProjectRepository projectRepository,
        ISavedViewRepository savedViewRepository,
        ISessionTokenService sessionTokenService,
        ISyncService syncService,
        ITaskItemRepository taskItemRepository,
        ITaskTemplateRepository taskTemplateRepository,
        IOptions<UsageOptions> usageOptions)
    {
        _clock = clock;
        _authRepository = authRepository;
        _currentUserSessionProvider = currentUserSessionProvider;
        _developmentWorkspaceProvider = developmentWorkspaceProvider;
        _liveUpdatePublisher = liveUpdatePublisher;
        _projectRepository = projectRepository;
        _savedViewRepository = savedViewRepository;
        _sessionTokenService = sessionTokenService;
        _syncService = syncService;
        _taskItemRepository = taskItemRepository;
        _taskTemplateRepository = taskTemplateRepository;
        _usageOptions = usageOptions;
    }

    public async Task<TaskItemDetailResponse> CreateAsync(
        CreateTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await GetRequiredWorkspaceContextAsync(cancellationToken);
        if (context.IsSharedOnly)
        {
            throw new ValidationException("Task-share access cannot create tasks in this board.");
        }

        EnsureCanWriteWorkspace(context);

        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);
        TaskItem? parentTaskItem = null;
        if (request.ParentTaskItemId.HasValue)
        {
            if (request.ParentTaskItemId.Value == Guid.Empty)
            {
                throw new ValidationException("ParentTaskItemId cannot be empty.");
            }

            parentTaskItem = await _taskItemRepository.GetByIdAsync(
                request.ParentTaskItemId.Value,
                context.WorkspaceId,
                projectId: null,
                trackChanges: true,
                cancellationToken);

            if (parentTaskItem is null || !CanEditTask(context, currentSession, parentTaskItem))
            {
                throw new ValidationException("Parent task was not found.");
            }

            if (parentTaskItem.ParentTaskItemId.HasValue)
            {
                throw new ValidationException("Nested subtasks are not supported.");
            }
        }

        if (request.ClientGeneratedId.HasValue)
        {
            if (request.ClientGeneratedId.Value == Guid.Empty)
            {
                throw new ValidationException("ClientGeneratedId cannot be empty.");
            }

            var existingTaskItem = await _taskItemRepository.GetByIdAsync(
                request.ClientGeneratedId.Value,
                context.WorkspaceId,
                projectId: null,
                trackChanges: false,
                cancellationToken);

            if (existingTaskItem is not null)
            {
                if (existingTaskItem.ParentTaskItemId != request.ParentTaskItemId)
                {
                    throw new ValidationException(
                        "ClientGeneratedId already belongs to a task with a different parent.");
                }

                var existingTemplate = await ResolveTaskTemplateForDetailAsync(
                    existingTaskItem,
                    includeDeleted: true,
                    cancellationToken);

                return MapDetail(
                    existingTaskItem,
                    existingTemplate,
                    await GetTaskSyncStateAsync(
                        existingTaskItem.WorkspaceId,
                        existingTaskItem.Id,
                        cancellationToken));
            }
        }

        await EnsureTaskQuotaAsync(context.WorkspaceId, cancellationToken);
        var now = _clock.UtcNow;
        var taskTemplate = await ResolveTaskTemplateForCreateAsync(
            request.TaskTemplateId,
            cancellationToken);
        var project = await ResolveProjectAsync(
            context.WorkspaceId,
            parentTaskItem?.ProjectId ?? request.ProjectId,
            cancellationToken);
        var taskItem = request.ClientGeneratedId.HasValue
            ? TaskItem.Create(
                request.ClientGeneratedId.Value,
                context.WorkspaceId,
                project?.Id,
                request.Title,
                now,
                taskTemplate?.Id)
            : TaskItem.Create(
                context.WorkspaceId,
                project?.Id,
                request.Title,
                now,
                taskTemplate?.Id);

        if (parentTaskItem is not null)
        {
            taskItem.MakeSubtaskOf(parentTaskItem, now);
            parentTaskItem.RecordSubtaskAdded(taskItem, now);
        }
        var category = string.IsNullOrWhiteSpace(request.Category)
            ? parentTaskItem?.Category ?? (request.ProjectId.HasValue ? project?.Name : null)
            : request.Category;

        if (!string.IsNullOrWhiteSpace(category))
        {
            taskItem.ChangeCategory(category, now);
        }

        ValidateRequiredFieldValues(taskTemplate, request.FieldValues);
        ApplyFieldValues(taskItem, taskTemplate, request.FieldValues, now);

        await _taskItemRepository.AddAsync(taskItem, cancellationToken);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);
        await _syncService.EnsureLocalTaskMappingAsync(
            context.WorkspaceId,
            taskItem.Id,
            cancellationToken);
        await PublishTaskEventAsync(
            LiveUpdateEvents.TaskCreated,
            taskItem,
            now,
            cancellationToken);
        if (parentTaskItem is not null)
        {
            await PublishTaskEventAsync(
                LiveUpdateEvents.TaskUpdated,
                parentTaskItem,
                now,
                cancellationToken);
        }

        return MapDetail(
            taskItem,
            taskTemplate,
            await GetTaskSyncStateAsync(taskItem.WorkspaceId, taskItem.Id, cancellationToken));
    }

    public async Task<TaskItemDetailResponse?> CreateSubtaskAsync(
        Guid parentTaskItemId,
        CreateTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ParentTaskItemId.HasValue && request.ParentTaskItemId.Value != parentTaskItemId)
        {
            throw new ValidationException("The parent task does not match the route.");
        }

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);
        var parent = await _taskItemRepository.GetByIdAsync(
            parentTaskItemId,
            context.WorkspaceId,
            projectId: null,
            trackChanges: false,
            cancellationToken);
        if (parent is null || !CanEditTask(context, currentSession, parent))
        {
            return null;
        }

        return await CreateAsync(
            request with { ParentTaskItemId = parentTaskItemId },
            cancellationToken);
    }

    public async Task<IReadOnlyList<TaskItemSummaryResponse>?> ListSubtasksAsync(
        Guid parentTaskItemId,
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);
        var parent = await _taskItemRepository.GetByIdAsync(
            parentTaskItemId,
            context.WorkspaceId,
            projectId: null,
            trackChanges: false,
            cancellationToken);

        if (parent is null || !CanReadTask(context, currentSession, parent))
        {
            return null;
        }

        var query = await BuildQueryAsync(
            context.WorkspaceId,
            context.IsSharedOnly,
            new TaskItemListRequest(Scope: TaskItemListScope.All),
            cancellationToken);
        query = query with { ParentTaskItemId = parentTaskItemId };
        var subtasks = await _taskItemRepository.ListAsync(query, cancellationToken);
        var syncStates = await _syncService.ListTaskSyncStatesAsync(
            context.WorkspaceId,
            subtasks.Select(taskItem => taskItem.Id).ToArray(),
            cancellationToken);
        var taskTemplates = await LoadSummaryTemplatesAsync(subtasks, cancellationToken);

        return subtasks
            .Select(taskItem => MapSummary(
                taskItem,
                syncStates.GetValueOrDefault(taskItem.Id),
                taskTemplate: taskItem.TaskTemplateId.HasValue
                    ? taskTemplates.GetValueOrDefault(taskItem.TaskTemplateId.Value)
                    : null))
            .ToList();
    }

    private async Task<DevelopmentWorkspaceContext> GetRequiredWorkspaceContextAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new ValidationException("Create a board before creating tasks.", exception);
        }
    }

    private async Task EnsureTaskQuotaAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var usageOptions = _usageOptions.Value;
        var activeTaskCount = await _taskItemRepository.CountAsync(
            workspaceId,
            includeArchived: false,
            cancellationToken);
        var totalTaskCount = await _taskItemRepository.CountAsync(
            workspaceId,
            includeArchived: true,
            cancellationToken);

        if (activeTaskCount >= usageOptions.MaxActiveTasksPerWorkspace)
        {
            throw new ValidationException(
                $"This workspace has reached the active task limit of {usageOptions.MaxActiveTasksPerWorkspace}.");
        }

        if (totalTaskCount >= usageOptions.MaxTotalTasksPerWorkspace)
        {
            throw new ValidationException(
                $"This workspace has reached the total task limit of {usageOptions.MaxTotalTasksPerWorkspace}.");
        }
    }

    public async Task<IReadOnlyList<TaskItemSummaryResponse>> ListAsync(
        TaskItemListScope scope,
        CancellationToken cancellationToken)
    {
        return await ListAsync(
            new TaskItemListRequest(Scope: scope),
            cancellationToken);
    }

    public async Task<IReadOnlyList<TaskItemSummaryResponse>> ListAsync(
        TaskItemListRequest request,
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var query = await BuildQueryAsync(
            context.WorkspaceId,
            context.IsSharedOnly,
            request,
            cancellationToken);
        var taskItems = await _taskItemRepository.ListAsync(
            query,
            cancellationToken);

        var syncStates = await _syncService.ListTaskSyncStatesAsync(
            context.WorkspaceId,
            taskItems.Select(taskItem => taskItem.Id).ToArray(),
            cancellationToken);
        var subtaskCounts = await _taskItemRepository.CountChildrenByParentIdsAsync(
            context.WorkspaceId,
            taskItems.Select(taskItem => taskItem.Id).ToArray(),
            cancellationToken);
        var taskTemplates = await LoadSummaryTemplatesAsync(taskItems, cancellationToken);

        return taskItems
            .Select(taskItem => MapSummary(
                taskItem,
                syncStates.GetValueOrDefault(taskItem.Id),
                subtaskCounts.GetValueOrDefault(taskItem.Id),
                taskItem.TaskTemplateId.HasValue
                    ? taskTemplates.GetValueOrDefault(taskItem.TaskTemplateId.Value)
                    : null))
            .ToList();
    }

    public async Task<IReadOnlyList<TaskItemViewCountResponse>> CountByViewsAsync(
        IReadOnlyList<Guid> viewIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewIds);

        var normalizedViewIds = viewIds
            .Where(viewId => viewId != Guid.Empty)
            .Distinct()
            .ToArray();

        if (normalizedViewIds.Length == 0)
        {
            return [];
        }

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var queries = new Dictionary<Guid, TaskItemQuery>();

        foreach (var viewId in normalizedViewIds)
        {
            queries[viewId] = await BuildQueryAsync(
                context.WorkspaceId,
                context.IsSharedOnly,
                new TaskItemListRequest(ViewId: viewId),
                cancellationToken);
        }

        var counts = await _taskItemRepository.CountByQueriesAsync(
            queries,
            cancellationToken);

        return normalizedViewIds
            .Select(viewId => new TaskItemViewCountResponse(
                viewId,
                counts.TryGetValue(viewId, out var count) ? count : 0))
            .ToList();
    }

    public async Task<TaskItemDetailResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);
        var taskItem = await _taskItemRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            projectId: null,
            trackChanges: true,
            cancellationToken);

        if (taskItem is null)
        {
            return null;
        }

        if (!CanReadTask(context, currentSession, taskItem))
        {
            return null;
        }

        taskItem.MarkViewed(_clock.UtcNow);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);

        var taskTemplate = await ResolveTaskTemplateForDetailAsync(
            taskItem,
            includeDeleted: true,
            cancellationToken);
        var subtaskCounts = await _taskItemRepository.CountChildrenByParentIdsAsync(
            context.WorkspaceId,
            [taskItem.Id],
            cancellationToken);

        return MapDetail(
            taskItem,
            taskTemplate,
            await GetTaskSyncStateAsync(taskItem.WorkspaceId, taskItem.Id, cancellationToken),
            subtaskCounts.GetValueOrDefault(taskItem.Id));
    }

    public async Task<TaskItemDetailResponse?> UpdateAsync(
        Guid id,
        UpdateTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);
        var taskItem = await _taskItemRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            projectId: null,
            trackChanges: true,
            cancellationToken);

        if (taskItem is null)
        {
            return null;
        }

        if (!CanEditTask(context, currentSession, taskItem))
        {
            return null;
        }

        var now = _clock.UtcNow;
        var taskTemplate = await ResolveTaskTemplateForDetailAsync(
            taskItem,
            includeDeleted: true,
            cancellationToken);
        Project? project = null;

        if (request.ProjectId.HasValue)
        {
            project = await ResolveProjectAsync(
                context.WorkspaceId,
                request.ProjectId,
                cancellationToken);
            taskItem.AssignProject(project?.Id);
        }
        else if (request.Category is not null && string.IsNullOrWhiteSpace(request.Category))
        {
            taskItem.AssignProject(null);
        }

        if (request.Title is not null)
        {
            taskItem.Rename(request.Title, now);
        }

        if (request.Status is not null)
        {
            taskItem.ChangeStatus(request.Status, now);
        }

        if (request.Category is not null)
        {
            taskItem.ChangeCategory(
                string.IsNullOrWhiteSpace(request.Category)
                    ? project?.Name
                    : request.Category,
                now);
        }
        else if (project is not null)
        {
            taskItem.ChangeCategory(project.Name, now);
        }

        if (request.Color is not null)
        {
            taskItem.ChangeColor(request.Color, now);
        }

        if (request.FollowUpAt.HasValue)
        {
            taskItem.SetFollowUp(request.FollowUpAt.Value, now);
        }

        ApplyFieldValues(taskItem, taskTemplate, request.FieldValues, now);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);
        await PublishTaskEventAsync(
            LiveUpdateEvents.TaskUpdated,
            taskItem,
            now,
            cancellationToken,
            currentSession);

        return MapDetail(taskItem, taskTemplate, await GetTaskSyncStateAsync(taskItem.WorkspaceId, taskItem.Id, cancellationToken));
    }

    public async Task<CopyTaskItemsResponse> CopyAsync(
        CopyTaskItemsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestedTaskIds = request.TaskItemIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (requestedTaskIds.Length == 0)
        {
            throw new ValidationException("At least one task is required.");
        }

        if (request.DestinationWorkspaceId == Guid.Empty)
        {
            throw new ValidationException("Destination workspace is required.");
        }

        var sourceContext = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        var destinationWorkspace = (await _authRepository.ListWorkspacesForUserAsync(
                currentSession.UserId,
                cancellationToken))
            .SingleOrDefault(workspace =>
                workspace.Workspace.Id == request.DestinationWorkspaceId);

        if (destinationWorkspace is null ||
            destinationWorkspace.AccessKind != WorkspaceAccessKinds.Membership ||
            destinationWorkspace.Membership.Role == WorkspaceMembershipRole.ReadOnly)
        {
            throw new ValidationException("Destination board was not found.");
        }

        var explicitlySelectedTasks = await _taskItemRepository.ListByIdsAsync(
            sourceContext.WorkspaceId,
            requestedTaskIds,
            trackChanges: false,
            cancellationToken);

        if (explicitlySelectedTasks.Count != requestedTaskIds.Length)
        {
            throw new ValidationException("One or more selected tasks were not found.");
        }

        var inaccessibleTask = explicitlySelectedTasks.FirstOrDefault(taskItem =>
            !CanReadTask(sourceContext, currentSession, taskItem));

        if (inaccessibleTask is not null)
        {
            throw new ValidationException("One or more selected tasks were not found.");
        }

        var selectedParentIds = explicitlySelectedTasks
            .Where(taskItem => taskItem.ParentTaskItemId is null)
            .Select(taskItem => taskItem.Id)
            .ToArray();
        var readableChildren = (await _taskItemRepository.ListChildrenByParentIdsAsync(
                sourceContext.WorkspaceId,
                selectedParentIds,
                trackChanges: false,
                cancellationToken))
            .Where(taskItem => CanReadTask(sourceContext, currentSession, taskItem));
        var sourceTasks = explicitlySelectedTasks
            .Concat(readableChildren)
            .DistinctBy(taskItem => taskItem.Id)
            .ToArray();

        var now = _clock.UtcNow;
        var copiedTasks = new List<TaskItemDetailResponse>();
        var copiedTaskEntitiesBySourceId = new Dictionary<Guid, TaskItem>();
        var copiedTemplatesBySourceId = new Dictionary<Guid, CopiedTemplate>();

        foreach (var sourceTask in sourceTasks
                     .OrderBy(task => task.ParentTaskItemId.HasValue)
                     .ThenBy(task => Array.IndexOf(requestedTaskIds, task.Id)))
        {
            await EnsureTaskQuotaAsync(request.DestinationWorkspaceId, cancellationToken);

            var sourceTemplate = await ResolveTaskTemplateForDetailAsync(
                sourceTask,
                includeDeleted: false,
                cancellationToken);
            CopiedTemplate? templateCopy = null;

            if (sourceTemplate is not null &&
                !copiedTemplatesBySourceId.TryGetValue(sourceTemplate.Id, out templateCopy))
            {
                templateCopy = await ResolveTemplateForCopiedTaskAsync(
                    sourceTemplate,
                    currentSession.UserId,
                    now,
                    cancellationToken);
                copiedTemplatesBySourceId[sourceTemplate.Id] = templateCopy;
            }

            var copyWithinSameWorkspace = sourceTask.WorkspaceId == request.DestinationWorkspaceId;
            var destinationProjectId = copyWithinSameWorkspace ? sourceTask.ProjectId : null;
            var copiedTask = TaskItem.Create(
                request.DestinationWorkspaceId,
                destinationProjectId,
                sourceTask.Title,
                now,
                templateCopy?.Template.Id);

            if (!string.IsNullOrWhiteSpace(sourceTask.Category))
            {
                copiedTask.ChangeCategory(sourceTask.Category, now);
            }

            if (!string.IsNullOrWhiteSpace(sourceTask.Status))
            {
                copiedTask.ChangeStatus(sourceTask.Status, now);
            }

            if (!string.IsNullOrWhiteSpace(sourceTask.Color))
            {
                copiedTask.ChangeColor(sourceTask.Color, now);
            }

            if (sourceTask.FollowUpAt.HasValue)
            {
                copiedTask.SetFollowUp(sourceTask.FollowUpAt.Value, now);
            }

            if (templateCopy is not null)
            {
                CopyFieldValues(sourceTask, copiedTask, templateCopy.FieldMap, now);
            }

            copiedTask.AddNote($"Copied from \"{sourceTask.Title}\".", now);

            if (request.IncludeTimeline)
            {
                CopyTimelineNotes(sourceTask, copiedTask, templateCopy?.FieldMap, now);
            }

            if (sourceTask.ParentTaskItemId.HasValue &&
                copiedTaskEntitiesBySourceId.TryGetValue(
                    sourceTask.ParentTaskItemId.Value,
                    out var copiedParent))
            {
                copiedTask.MakeSubtaskOf(copiedParent, now);
                copiedParent.RecordSubtaskAdded(copiedTask, now);
            }

            await _taskItemRepository.AddAsync(copiedTask, cancellationToken);
            copiedTaskEntitiesBySourceId[sourceTask.Id] = copiedTask;
            copiedTasks.Add(MapDetail(copiedTask, templateCopy?.Template));
        }

        await _taskItemRepository.SaveChangesAsync(cancellationToken);

        foreach (var copiedTask in copiedTasks)
        {
            await _syncService.EnsureLocalTaskMappingAsync(
                copiedTask.WorkspaceId,
                copiedTask.Id,
                cancellationToken);
        }

        var copiedSyncStates = await _syncService.ListTaskSyncStatesAsync(
            request.DestinationWorkspaceId,
            copiedTasks.Select(copiedTask => copiedTask.Id).ToArray(),
            cancellationToken);
        copiedTasks = copiedTasks
            .Select(copiedTask => copiedTask with
            {
                SyncState = copiedSyncStates.GetValueOrDefault(copiedTask.Id)
            })
            .ToList();

        foreach (var copiedTask in copiedTasks)
        {
            await _liveUpdatePublisher.PublishAsync(
                new LiveUpdateMessage(
                    LiveUpdateEvents.TaskCreated,
                    copiedTask.WorkspaceId,
                    copiedTask.Id,
                    null,
                    currentSession.UserId,
                    now,
                    copiedTask.LastTouchedAt),
                cancellationToken);
        }

        return new CopyTaskItemsResponse(copiedTasks);
    }

    public async Task<TaskTemplateImportResponse?> ImportTemplateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var taskItem = await GetTaskItemForReadAsync(id, cancellationToken);

        if (taskItem is null)
        {
            return null;
        }

        var sourceTemplate = await ResolveTaskTemplateForDetailAsync(
            taskItem,
            includeDeleted: true,
            cancellationToken);

        if (sourceTemplate is null)
        {
            throw new ValidationException("Task does not have a template to import.");
        }

        var ownerUserId = await GetTaskTemplateImportOwnerUserIdAsync(cancellationToken);
        var copiedTemplate = await ResolveTemplateForCopiedTaskAsync(
            sourceTemplate,
            ownerUserId,
            _clock.UtcNow,
            cancellationToken,
            GetTaskFieldValueDefinitionIds(taskItem));

        await _taskTemplateRepository.SaveChangesAsync(cancellationToken);

        return new TaskTemplateImportResponse(
            sourceTemplate.Id,
            TaskTemplateService.MapDetail(copiedTemplate.Template));
    }

    public async Task<TaskItemDetailResponse?> UpdateTimelineEntryAsync(
        Guid taskItemId,
        Guid entryId,
        UpdateTaskTimelineEntryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var taskItem = await GetTaskItemForUpdateAsync(taskItemId, cancellationToken);

        if (taskItem is null)
        {
            return null;
        }

        if (request.Note is null && (request.FieldValues is null || request.FieldValues.Count == 0))
        {
            throw new ValidationException("Note text or entry fields are required.");
        }

        var now = _clock.UtcNow;
        var taskTemplate = await ResolveTaskTemplateForDetailAsync(
            taskItem,
            includeDeleted: true,
            cancellationToken);

        if (request.Note is not null)
        {
            taskItem.EditNote(entryId, request.Note, now);
        }

        ApplyTimelineEntryFieldValues(
            taskItem,
            entryId,
            taskTemplate,
            request.FieldValues,
            now,
            requireRequiredFields: false);

        await _taskItemRepository.SaveChangesAsync(cancellationToken);
        await PublishTaskEventAsync(
            LiveUpdateEvents.NoteEdited,
            taskItem,
            taskItem.LastTouchedAt,
            cancellationToken,
            timelineEntryId: entryId);

        return MapDetail(
            taskItem,
            taskTemplate,
            await GetTaskSyncStateAsync(taskItem.WorkspaceId, taskItem.Id, cancellationToken));
    }

    public async Task<TaskItemDetailResponse?> DeleteTimelineEntryAsync(
        Guid taskItemId,
        Guid entryId,
        CancellationToken cancellationToken)
    {
        var taskItem = await GetTaskItemForUpdateAsync(taskItemId, cancellationToken);

        if (taskItem is null)
        {
            return null;
        }

        taskItem.DeleteNote(entryId, _clock.UtcNow);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);
        await PublishTaskEventAsync(
            LiveUpdateEvents.NoteDeleted,
            taskItem,
            taskItem.LastTouchedAt,
            cancellationToken,
            timelineEntryId: entryId);

        var taskTemplate = await ResolveTaskTemplateForDetailAsync(
            taskItem,
            includeDeleted: true,
            cancellationToken);

        return MapDetail(taskItem, taskTemplate, await GetTaskSyncStateAsync(taskItem.WorkspaceId, taskItem.Id, cancellationToken));
    }

    public async Task<TaskItemDetailResponse?> AddTimelineEntryAsync(
        Guid id,
        AddTaskTimelineEntryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var taskItem = await GetTaskItemForUpdateAsync(id, cancellationToken);

        if (taskItem is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var taskTemplate = await ResolveTaskTemplateForDetailAsync(
            taskItem,
            includeDeleted: true,
            cancellationToken);
        ValidateTimelineEntryHasContent(request);
        if (request.ClientGeneratedId.HasValue &&
            request.ClientGeneratedId.Value == Guid.Empty)
        {
            throw new ValidationException("ClientGeneratedId cannot be empty.");
        }

        if (request.ClientGeneratedId.HasValue &&
            taskItem.TimelineEntries.Any(entry => entry.Id == request.ClientGeneratedId.Value))
        {
            return MapDetail(
                taskItem,
                taskTemplate,
                await GetTaskSyncStateAsync(taskItem.WorkspaceId, taskItem.Id, cancellationToken));
        }

        var entry = request.ClientGeneratedId.HasValue
            ? taskItem.AddNote(request.ClientGeneratedId.Value, request.Note, now)
            : taskItem.AddNote(request.Note, now);
        ApplyTimelineEntryFieldValues(
            taskItem,
            entry.Id,
            taskTemplate,
            request.FieldValues,
            now,
            requireRequiredFields: true);

        await _taskItemRepository.SaveChangesAsync(cancellationToken);
        await PublishTaskEventAsync(
            LiveUpdateEvents.NoteAdded,
            taskItem,
            now,
            cancellationToken);

        return MapDetail(taskItem, taskTemplate, await GetTaskSyncStateAsync(taskItem.WorkspaceId, taskItem.Id, cancellationToken));
    }

    public async Task<TaskItemDetailResponse?> ArchiveAsync(
        Guid id,
        ArchiveTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.ArchiveResolutionId.HasValue ||
            request.ArchiveResolutionId.Value == Guid.Empty)
        {
            throw new ValidationException("ArchiveResolutionId is required.");
        }

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);
        var taskItem = await _taskItemRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            projectId: null,
            trackChanges: true,
            cancellationToken);

        if (taskItem is null)
        {
            return null;
        }

        if (!CanEditTask(context, currentSession, taskItem))
        {
            return null;
        }

        var archiveResolution = await _taskItemRepository.GetArchiveResolutionByIdAsync(
            request.ArchiveResolutionId.Value,
            context.WorkspaceId,
            cancellationToken);

        if (archiveResolution is null)
        {
            throw new ValidationException("Archive resolution was not found.");
        }

        taskItem.Archive(archiveResolution, _clock.UtcNow, request.Note);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);
        await PublishTaskEventAsync(
            LiveUpdateEvents.TaskUpdated,
            taskItem,
            taskItem.LastTouchedAt,
            cancellationToken,
            currentSession);

        var taskTemplate = await ResolveTaskTemplateForDetailAsync(
            taskItem,
            includeDeleted: true,
            cancellationToken);

        return MapDetail(taskItem, taskTemplate, await GetTaskSyncStateAsync(taskItem.WorkspaceId, taskItem.Id, cancellationToken));
    }

    public async Task<TaskItemDetailResponse?> ReopenAsync(
        Guid id,
        ReopenTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var taskItem = await GetTaskItemForUpdateAsync(id, cancellationToken);

        if (taskItem is null)
        {
            return null;
        }

        taskItem.Reopen(_clock.UtcNow, request.Note);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);
        await PublishTaskEventAsync(
            LiveUpdateEvents.TaskUpdated,
            taskItem,
            taskItem.LastTouchedAt,
            cancellationToken);

        var taskTemplate = await ResolveTaskTemplateForDetailAsync(
            taskItem,
            includeDeleted: true,
            cancellationToken);

        return MapDetail(taskItem, taskTemplate, await GetTaskSyncStateAsync(taskItem.WorkspaceId, taskItem.Id, cancellationToken));
    }

    public async Task<TaskItemBatchResponse> ReopenAsync(
        ReopenTaskItemsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var taskIds = NormalizeTaskIds(request.TaskItemIds);
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);
        var taskItems = await _taskItemRepository.ListByIdsAsync(
            context.WorkspaceId,
            taskIds,
            trackChanges: true,
            cancellationToken);

        if (taskItems.Count != taskIds.Count ||
            taskItems.Any(taskItem => !CanEditTask(context, currentSession, taskItem)))
        {
            throw new ValidationException("One or more selected tasks were not found.");
        }

        if (taskItems.Any(taskItem => taskItem.ArchivedAt is null))
        {
            throw new ValidationException("Only archived tasks can be unarchived.");
        }

        var now = _clock.UtcNow;

        foreach (var taskItem in taskItems)
        {
            taskItem.Reopen(now, request.Note);
        }

        await _taskItemRepository.SaveChangesAsync(cancellationToken);

        foreach (var taskItem in taskItems)
        {
            await PublishTaskEventAsync(
                LiveUpdateEvents.TaskUpdated,
                taskItem,
                now,
                cancellationToken,
                currentSession);
        }

        return new TaskItemBatchResponse(taskItems.Count);
    }

    public async Task<TaskItemBatchResponse> DeleteArchivedAsync(
        DeleteTaskItemsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var taskIds = NormalizeTaskIds(request.TaskItemIds);
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);

        if (!context.CanDeleteWorkspaceData)
        {
            throw new ValidationException("Only the board owner can permanently delete archived tasks.");
        }

        var explicitlySelectedTaskItems = await _taskItemRepository.ListByIdsAsync(
            context.WorkspaceId,
            taskIds,
            trackChanges: false,
            cancellationToken);

        if (explicitlySelectedTaskItems.Count != taskIds.Count)
        {
            throw new ValidationException("One or more selected tasks were not found.");
        }

        var selectedParentIds = explicitlySelectedTaskItems
            .Where(taskItem => taskItem.ParentTaskItemId is null)
            .Select(taskItem => taskItem.Id)
            .ToArray();
        var childTaskItems = await _taskItemRepository.ListChildrenByParentIdsAsync(
            context.WorkspaceId,
            selectedParentIds,
            trackChanges: false,
            cancellationToken);
        var taskItems = explicitlySelectedTaskItems
            .Concat(childTaskItems)
            .DistinctBy(taskItem => taskItem.Id)
            .ToArray();

        if (taskItems.Any(taskItem => taskItem.ArchivedAt is null))
        {
            throw new ValidationException(
                "Archive the task and all of its subtasks before permanently deleting it.");
        }

        var deletedCount = await _taskItemRepository.DeleteArchivedAsync(
            context.WorkspaceId,
            taskItems.Select(taskItem => taskItem.Id).ToArray(),
            cancellationToken);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);

        return new TaskItemBatchResponse(deletedCount);
    }

    public async Task<IReadOnlyList<TaskItemShareResponse>?> ListSharesAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var taskItem = await GetTaskItemForReadAsync(id, cancellationToken);
        return taskItem is null ? null : MapShares(taskItem);
    }

    public async Task<IReadOnlyList<TaskShareInboxResponse>> ListIncomingSharesAsync(
        CancellationToken cancellationToken)
    {
        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        var shares = await _taskItemRepository.ListIncomingSharesAsync(
            currentSession.UserId,
            AppUser.NormalizeEmail(currentSession.Email),
            cancellationToken);

        var now = _clock.UtcNow;

        return shares
            .Where(item => item.Share.ExpiresAt is null || item.Share.ExpiresAt > now)
            .Select(MapIncomingShare)
            .ToList();
    }

    public async Task<TaskItemDetailResponse?> ShareAsync(
        Guid id,
        CreateTaskShareRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        var taskItem = await GetTaskItemForShareManagementAsync(id, cancellationToken);

        if (taskItem is null)
        {
            return null;
        }

        var targetUser = await _authRepository.GetUserByNormalizedEmailAsync(
            AppUser.NormalizeEmail(request.Email),
            trackChanges: false,
            cancellationToken);

        if (targetUser is not null && !targetUser.IsActive)
        {
            throw new ValidationException("User cannot be shared with.");
        }

        taskItem.AddShare(
            request.Email,
            targetUser?.Id,
            currentSession.UserId,
            request.Role,
            tokenHash: null,
            expiresAt: null,
            _clock.UtcNow);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);
        await PublishTaskEventAsync(
            LiveUpdateEvents.TaskShared,
            taskItem,
            taskItem.LastTouchedAt,
            cancellationToken,
            currentSession,
            recipientUserIds: targetUser?.Id is null ? null : [targetUser.Id]);

        var taskTemplate = await ResolveTaskTemplateForDetailAsync(
            taskItem,
            includeDeleted: true,
            cancellationToken);

        return MapDetail(taskItem, taskTemplate, await GetTaskSyncStateAsync(taskItem.WorkspaceId, taskItem.Id, cancellationToken));
    }

    public async Task<TaskShareLinkResponse?> CreateShareLinkAsync(
        Guid id,
        CreateTaskShareRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await CreateShareLinkAsync(
            new CreateTaskShareLinkRequest(
                request.Email,
                [id],
                request.Role),
            cancellationToken);

        return response;
    }

    public async Task<TaskShareLinkResponse> CreateShareLinkAsync(
        CreateTaskShareLinkRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        EnsureCanManageTaskSharing(context);

        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        var requestedTaskIds = (request.TaskItemIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (requestedTaskIds.Length == 0)
        {
            throw new ValidationException("At least one task is required.");
        }

        var taskItems = await _taskItemRepository.ListByIdsAsync(
            context.WorkspaceId,
            requestedTaskIds,
            trackChanges: true,
            cancellationToken);

        if (taskItems.Count != requestedTaskIds.Length)
        {
            throw new ValidationException("One or more selected tasks were not found.");
        }

        var normalizedEmail = AppUser.NormalizeEmail(request.Email);
        var targetUser = await _authRepository.GetUserByNormalizedEmailAsync(
            normalizedEmail,
            trackChanges: false,
            cancellationToken);

        if (targetUser is not null && !targetUser.IsActive)
        {
            throw new ValidationException("User cannot be shared with.");
        }

        if (taskItems.Any(taskItem => taskItem.Shares.Any(share =>
                share.RevokedAt is null &&
                string.Equals(share.NormalizedEmail, normalizedEmail, StringComparison.Ordinal))))
        {
            throw new ValidationException("A pending or active task share already exists for this email.");
        }

        var now = _clock.UtcNow;
        var token = _sessionTokenService.CreateSessionToken();
        var tokenHash = _sessionTokenService.HashToken(token);
        var expiresAt = now.Add(ShareLinkLifetime);
        var shares = new List<TaskItemShareResponse>();

        foreach (var taskItem in taskItems)
        {
            var share = taskItem.AddShare(
                request.Email,
                targetUser?.Id,
                currentSession.UserId,
                request.Role,
                tokenHash,
                expiresAt,
                now);
            shares.Add(MapShare(share));
        }

        await _taskItemRepository.SaveChangesAsync(cancellationToken);

        foreach (var taskItem in taskItems)
        {
            await PublishTaskEventAsync(
                LiveUpdateEvents.TaskShared,
                taskItem,
                now,
                cancellationToken,
                currentSession,
                recipientUserIds: targetUser?.Id is null ? null : [targetUser.Id]);
        }

        return new TaskShareLinkResponse(
            shares.OrderBy(share => share.Email).ToList(),
            token,
            expiresAt);
    }

    public async Task<ShareLinkAcceptResponse> AcceptShareLinkAsync(
        AcceptShareLinkRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        var tokenHash = _sessionTokenService.HashToken(request.Token);
        var taskItems = await _taskItemRepository.ListByShareTokenHashAsync(
            tokenHash,
            trackChanges: true,
            cancellationToken);
        var now = _clock.UtcNow;
        var normalizedEmail = AppUser.NormalizeEmail(currentSession.Email);
        var acceptedTaskIds = new List<Guid>();
        Guid? workspaceId = null;

        foreach (var taskItem in taskItems)
        {
            var share = taskItem.Shares.FirstOrDefault(candidate =>
                string.Equals(candidate.TokenHash, tokenHash, StringComparison.Ordinal));

            if (share is null)
            {
                continue;
            }

            if (!share.IsUsable(now) ||
                !string.Equals(share.NormalizedEmail, normalizedEmail, StringComparison.Ordinal))
            {
                throw new ValidationException("Share link is invalid or expired.");
            }

            share.Accept(currentSession.UserId, now);
            acceptedTaskIds.Add(taskItem.Id);
            workspaceId ??= taskItem.WorkspaceId;
        }

        if (acceptedTaskIds.Count == 0 || !workspaceId.HasValue)
        {
            throw new ValidationException("Share link is invalid or expired.");
        }

        await _taskItemRepository.SaveChangesAsync(cancellationToken);

        foreach (var taskItem in taskItems)
        {
            await PublishTaskEventAsync(
                LiveUpdateEvents.TaskShared,
                taskItem,
                now,
                cancellationToken,
                currentSession,
                recipientUserIds: [currentSession.UserId]);
        }

        return new ShareLinkAcceptResponse(
            "Task",
            workspaceId.Value,
            acceptedTaskIds);
    }

    public async Task<TaskItemDetailResponse?> RevokeShareAsync(
        Guid id,
        Guid shareId,
        CancellationToken cancellationToken)
    {
        var taskItem = await GetTaskItemForShareManagementAsync(id, cancellationToken);

        if (taskItem is null)
        {
            return null;
        }

        var revokedRecipientUserId = taskItem.Shares
            .FirstOrDefault(share => share.Id == shareId)?
            .SharedWithUserId;

        taskItem.RevokeShare(shareId, _clock.UtcNow);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);
        await PublishTaskEventAsync(
            LiveUpdateEvents.TaskUpdated,
            taskItem,
            taskItem.LastTouchedAt,
            cancellationToken,
            recipientUserIds: revokedRecipientUserId.HasValue
                ? [revokedRecipientUserId.Value]
                : null);

        var taskTemplate = await ResolveTaskTemplateForDetailAsync(
            taskItem,
            includeDeleted: true,
            cancellationToken);

        return MapDetail(taskItem, taskTemplate, await GetTaskSyncStateAsync(taskItem.WorkspaceId, taskItem.Id, cancellationToken));
    }

    public async Task<TaskItemDetailResponse?> UpdateShareRoleAsync(
        Guid id,
        Guid shareId,
        UpdateTaskShareRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var taskItem = await GetTaskItemForShareManagementAsync(id, cancellationToken);

        if (taskItem is null)
        {
            return null;
        }

        taskItem.ChangeShareRole(shareId, request.Role, _clock.UtcNow);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);
        await PublishTaskEventAsync(
            LiveUpdateEvents.TaskUpdated,
            taskItem,
            taskItem.LastTouchedAt,
            cancellationToken);

        var taskTemplate = await ResolveTaskTemplateForDetailAsync(
            taskItem,
            includeDeleted: true,
            cancellationToken);

        return MapDetail(taskItem, taskTemplate, await GetTaskSyncStateAsync(taskItem.WorkspaceId, taskItem.Id, cancellationToken));
    }

    public async Task<bool> LeaveShareAsync(
        Guid shareId,
        CancellationToken cancellationToken)
    {
        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        var taskItem = await _taskItemRepository.GetByShareIdAsync(
            shareId,
            currentSession.UserId,
            AppUser.NormalizeEmail(currentSession.Email),
            trackChanges: true,
            cancellationToken);

        if (taskItem is null)
        {
            return false;
        }

        taskItem.RevokeShare(shareId, _clock.UtcNow);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);
        await PublishTaskEventAsync(
            LiveUpdateEvents.TaskUpdated,
            taskItem,
            taskItem.LastTouchedAt,
            cancellationToken,
            currentSession,
            recipientUserIds: [currentSession.UserId]);

        return true;
    }

    public async Task<int> LeaveWorkspaceSharesAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id is required.", nameof(workspaceId));
        }

        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        var normalizedEmail = AppUser.NormalizeEmail(currentSession.Email);
        var taskItems = await _taskItemRepository.ListByWorkspaceSharesForUserAsync(
            workspaceId,
            currentSession.UserId,
            normalizedEmail,
            trackChanges: true,
            cancellationToken);
        var revokedAt = _clock.UtcNow;
        var revokedCount = 0;

        foreach (var taskItem in taskItems)
        {
            var shareIds = taskItem.Shares
                .Where(share =>
                    share.RevokedAt is null &&
                    (share.SharedWithUserId == currentSession.UserId ||
                        share.NormalizedEmail == normalizedEmail))
                .Select(share => share.Id)
                .ToList();

            foreach (var shareId in shareIds)
            {
                taskItem.RevokeShare(shareId, revokedAt);
                revokedCount++;
            }
        }

        if (revokedCount == 0)
        {
            return 0;
        }

        await _taskItemRepository.SaveChangesAsync(cancellationToken);

        foreach (var taskItem in taskItems)
        {
            await PublishTaskEventAsync(
                LiveUpdateEvents.TaskUpdated,
                taskItem,
                taskItem.LastTouchedAt,
                cancellationToken,
                currentSession,
                recipientUserIds: [currentSession.UserId]);
        }

        return revokedCount;
    }

    private async Task PublishTaskEventAsync(
        string eventName,
        TaskItem taskItem,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken,
        CurrentUserSession? currentSession = null,
        Guid? timelineEntryId = null,
        IReadOnlyList<Guid>? recipientUserIds = null)
    {
        currentSession ??= await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);
        var mergedRecipientUserIds = MergeRecipientUserIds(
            recipientUserIds,
            GetActiveTaskShareRecipientUserIds(taskItem));

        await _liveUpdatePublisher.PublishAsync(
            new LiveUpdateMessage(
                eventName,
                taskItem.WorkspaceId,
                taskItem.Id,
                timelineEntryId,
                currentSession?.UserId,
                occurredAt,
                taskItem.LastTouchedAt,
                mergedRecipientUserIds),
            cancellationToken);
    }

    private static IReadOnlyList<Guid>? GetActiveTaskShareRecipientUserIds(TaskItem taskItem)
    {
        var recipientUserIds = taskItem.Shares
            .Where(share => share.IsActive && share.SharedWithUserId.HasValue)
            .Select(share => share.SharedWithUserId!.Value)
            .Distinct()
            .ToArray();

        return recipientUserIds.Length == 0
            ? null
            : recipientUserIds;
    }

    private static IReadOnlyList<Guid>? MergeRecipientUserIds(
        IReadOnlyList<Guid>? first,
        IReadOnlyList<Guid>? second)
    {
        if ((first is null || first.Count == 0) &&
            (second is null || second.Count == 0))
        {
            return null;
        }

        return (first ?? [])
            .Concat(second ?? [])
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToArray();
    }

    private async Task<TaskItem?> GetTaskItemForReadAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);
        var taskItem = await _taskItemRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            projectId: null,
            trackChanges: true,
            cancellationToken);

        return taskItem is not null && CanReadTask(context, currentSession, taskItem)
            ? taskItem
            : null;
    }

    private async Task<TaskItem?> GetTaskItemForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);
        var taskItem = await _taskItemRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            projectId: null,
            trackChanges: true,
            cancellationToken);

        return taskItem is not null && CanEditTask(context, currentSession, taskItem)
            ? taskItem
            : null;
    }

    private async Task<TaskItem?> GetTaskItemForShareManagementAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);

        if (!context.CanManageWorkspaceSharing)
        {
            return null;
        }

        await RequireCurrentSessionAsync(cancellationToken);

        return await _taskItemRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            projectId: null,
            trackChanges: true,
            cancellationToken);
    }

    private async Task<CurrentUserSession> RequireCurrentSessionAsync(
        CancellationToken cancellationToken)
    {
        return await _currentUserSessionProvider.GetCurrentAsync(cancellationToken) ??
            throw new UnauthorizedAccessException("Authentication is required.");
    }

    private static bool CanReadTask(
        DevelopmentWorkspaceContext context,
        CurrentUserSession? currentSession,
        TaskItem taskItem)
    {
        if (!context.IsSharedOnly)
        {
            return true;
        }

        if (currentSession is null)
        {
            return false;
        }

        return taskItem.Shares.Any(share =>
            share.MatchesUser(
                currentSession.UserId,
                AppUser.NormalizeEmail(currentSession.Email)));
    }

    private static bool CanEditTask(
        DevelopmentWorkspaceContext context,
        CurrentUserSession? currentSession,
        TaskItem taskItem)
    {
        if (!context.IsSharedOnly && context.CanWriteWorkspace)
        {
            return true;
        }

        if (currentSession is null)
        {
            return false;
        }

        var normalizedEmail = AppUser.NormalizeEmail(currentSession.Email);
        return taskItem.Shares.Any(share =>
            share.Role == TaskItemShareRole.Editor &&
            share.MatchesUser(currentSession.UserId, normalizedEmail));
    }

    private static void EnsureCanWriteWorkspace(DevelopmentWorkspaceContext context)
    {
        if (!context.CanWriteWorkspace)
        {
            throw new ValidationException("Read-only board access cannot change tasks.");
        }
    }

    private static void EnsureCanManageTaskSharing(DevelopmentWorkspaceContext context)
    {
        if (!context.CanManageWorkspaceSharing)
        {
            throw new ValidationException("Only board owners can manage task sharing.");
        }
    }

    private static IReadOnlyList<Guid> NormalizeTaskIds(IReadOnlyList<Guid> taskItemIds)
    {
        var taskIds = (taskItemIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (taskIds.Length == 0)
        {
            throw new ValidationException("At least one task is required.");
        }

        return taskIds;
    }

    private async Task<TaskItemQuery> BuildQueryAsync(
        Guid workspaceId,
        bool limitToSharedAccess,
        TaskItemListRequest request,
        CancellationToken cancellationToken)
    {
        var filter = request.ViewId.HasValue && request.ViewId.Value != Guid.Empty
            ? await GetSavedViewFilterAsync(
                workspaceId,
                request.ViewId.Value,
                cancellationToken)
            : SavedViewPayloads.NormalizeFilter(
                new SavedViewFilterRequest(
                    request.ProjectId,
                    request.Status,
                    request.Category,
                    request.Color,
                    request.Archive ?? MapScopeToArchiveFilter(request.Scope),
                    request.FollowUp,
                    request.NotViewedSinceDays,
                    request.NotTouchedSinceDays,
                    request.Text));
        var sort = request.ViewId.HasValue && request.ViewId.Value != Guid.Empty
            ? await GetSavedViewSortAsync(
                workspaceId,
                request.ViewId.Value,
                cancellationToken)
            : SavedViewPayloads.NormalizeSort(
                new SavedViewSortRequest(request.Sort, request.Direction));

        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);

        return new TaskItemQuery(
            workspaceId,
            filter.ProjectId == Guid.Empty ? null : filter.ProjectId,
            filter.Status,
            filter.Category,
            NormalizeColorFilter(filter.Color),
            ParseArchiveFilter(filter.Archive),
            ParseFollowUpFilter(filter.FollowUp),
            filter.NotViewedSinceDays,
            filter.NotTouchedSinceDays,
            filter.Text,
            request.SharedWith,
            currentSession?.UserId,
            currentSession is null ? null : AppUser.NormalizeEmail(currentSession.Email),
            limitToSharedAccess,
            request.SharedWithMe,
            ParseSortField(sort.Field),
            string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase),
            _clock.UtcNow,
            ParentTaskItemId: null,
            request.IncludeChildTasks || limitToSharedAccess);
    }

    private async Task<SavedViewFilterRequest> GetSavedViewFilterAsync(
        Guid workspaceId,
        Guid viewId,
        CancellationToken cancellationToken)
    {
        var savedView = await _savedViewRepository.GetByIdAsync(
            viewId,
            workspaceId,
            trackChanges: false,
            cancellationToken);

        return savedView is null
            ? throw new ValidationException("Saved view was not found.")
            : SavedViewPayloads.DeserializeFilter(savedView.DefinitionJson);
    }

    private async Task<SavedViewSortRequest> GetSavedViewSortAsync(
        Guid workspaceId,
        Guid viewId,
        CancellationToken cancellationToken)
    {
        var savedView = await _savedViewRepository.GetByIdAsync(
            viewId,
            workspaceId,
            trackChanges: false,
            cancellationToken);

        return savedView is null
            ? throw new ValidationException("Saved view was not found.")
            : SavedViewPayloads.DeserializeSort(savedView.SortJson);
    }

    private static string MapScopeToArchiveFilter(TaskItemListScope scope)
    {
        return scope switch
        {
            TaskItemListScope.Archive => "Archived",
            TaskItemListScope.All => "All",
            _ => "Active"
        };
    }

    private static TaskItemArchiveFilter ParseArchiveFilter(string? archive)
    {
        return archive switch
        {
            "Archived" => TaskItemArchiveFilter.Archived,
            "All" => TaskItemArchiveFilter.All,
            _ => TaskItemArchiveFilter.Active
        };
    }

    private static TaskItemFollowUpFilter ParseFollowUpFilter(string? followUp)
    {
        return followUp switch
        {
            "Any" => TaskItemFollowUpFilter.Any,
            "Overdue" => TaskItemFollowUpFilter.Overdue,
            "Today" => TaskItemFollowUpFilter.Today,
            "ThisWeek" => TaskItemFollowUpFilter.ThisWeek,
            _ => TaskItemFollowUpFilter.None
        };
    }

    private static TaskItemSortField ParseSortField(string? sortField)
    {
        return sortField switch
        {
            "createdAt" => TaskItemSortField.CreatedAt,
            "followUpAt" => TaskItemSortField.FollowUpAt,
            "title" => TaskItemSortField.Title,
            "status" => TaskItemSortField.Status,
            _ => TaskItemSortField.LastTouchedAt
        };
    }

    private static string? NormalizeColorFilter(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        var normalizedColor = color.Trim();

        if (normalizedColor.Length != 7 ||
            normalizedColor[0] != '#' ||
            normalizedColor.Skip(1).Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ValidationException(
                "Color filter must be a hex color in #RRGGBB format.");
        }

        return normalizedColor.ToUpperInvariant();
    }


    private async Task<TaskTemplate?> ResolveTaskTemplateForCreateAsync(
        Guid? requestedTemplateId,
        CancellationToken cancellationToken)
    {
        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);
        var ownerUserId = currentSession?.UserId;

        if (requestedTemplateId.HasValue && requestedTemplateId.Value != Guid.Empty)
        {
            var requestedTemplate = await _taskItemRepository.GetTaskTemplateByIdAsync(
                requestedTemplateId.Value,
                includeDeleted: false,
                cancellationToken);

            if (requestedTemplate is null || requestedTemplate.OwnerUserId != ownerUserId)
            {
                throw new ValidationException("Task template was not found.");
            }

            return requestedTemplate;
        }

        return await _taskItemRepository.GetDefaultTaskTemplateAsync(
            ownerUserId,
            cancellationToken);
    }

    private async Task<TaskTemplate?> ResolveTaskTemplateForDetailAsync(
        TaskItem taskItem,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        if (!taskItem.TaskTemplateId.HasValue)
        {
            return null;
        }

        return await _taskItemRepository.GetTaskTemplateByIdAsync(
            taskItem.TaskTemplateId.Value,
            includeDeleted,
            cancellationToken);
    }

    private async Task<CopiedTemplate> ResolveTemplateForCopiedTaskAsync(
        TaskTemplate sourceTemplate,
        Guid? destinationOwnerUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        IReadOnlySet<Guid>? fieldDefinitionIdsToPreserve = null)
    {
        if (sourceTemplate.OwnerUserId == destinationOwnerUserId && sourceTemplate.IsActive)
        {
            return new CopiedTemplate(
                sourceTemplate,
                sourceTemplate.FieldDefinitions.ToDictionary(
                    field => field.Id,
                    field => field));
        }

        var templateName = await GenerateImportedTemplateNameAsync(
            destinationOwnerUserId,
            sourceTemplate.Name,
            cancellationToken);
        var copiedTemplate = TaskTemplate.Create(destinationOwnerUserId, templateName, now);
        copiedTemplate.UpdateLayout(
            sourceTemplate.HeaderLayoutJson,
            sourceTemplate.EntryLayoutJson,
            now);
        var fieldMap = new Dictionary<Guid, FieldDefinition>();
        var preservedFieldIds = fieldDefinitionIdsToPreserve ?? new HashSet<Guid>();

        foreach (var sourceField in sourceTemplate.FieldDefinitions
                     .Where(field => field.IsActive || preservedFieldIds.Contains(field.Id))
                     .OrderBy(field => field.Scope)
                     .ThenBy(field => field.SortOrder)
                     .ThenBy(field => field.Label))
        {
            var copiedField = copiedTemplate.AddFieldDefinition(
                sourceField.Key,
                sourceField.Label,
                sourceField.Type,
                sourceField.Scope,
                sourceField.IsRequired,
                sourceField.SortOrder,
                sourceField.OptionsJson,
                sourceField.LayoutRow,
                sourceField.LayoutColumn,
                sourceField.LayoutRowSpan,
                sourceField.LayoutColumnSpan,
                sourceField.LayoutWeight);
            fieldMap[sourceField.Id] = copiedField;
        }

        await _taskTemplateRepository.AddAsync(copiedTemplate, cancellationToken);

        return new CopiedTemplate(copiedTemplate, fieldMap);
    }

    private async Task<string> GenerateImportedTemplateNameAsync(
        Guid? ownerUserId,
        string sourceName,
        CancellationToken cancellationToken)
    {
        var baseName = $"{sourceName} (imported)";
        var candidateName = baseName;
        var suffix = 2;

        while (await _taskTemplateRepository.AnyActiveWithNameAsync(
                   ownerUserId,
                   candidateName,
                   excludedTemplateId: null,
                   cancellationToken))
        {
            candidateName = $"{baseName} {suffix}";
            suffix += 1;
        }

        return candidateName;
    }

    private async Task<Guid?> GetTaskTemplateImportOwnerUserIdAsync(CancellationToken cancellationToken)
    {
        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);

        if (currentSession is not null)
        {
            return currentSession.UserId;
        }

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        EnsureCanWriteWorkspace(context);

        return null;
    }

    private async Task<Project?> ResolveProjectAsync(
        Guid workspaceId,
        Guid? requestedProjectId,
        CancellationToken cancellationToken)
    {
        if (!requestedProjectId.HasValue)
        {
            return null;
        }

        if (requestedProjectId.Value == Guid.Empty)
        {
            return null;
        }

        return await _projectRepository.GetByIdAsync(
                requestedProjectId.Value,
                workspaceId,
                cancellationToken) ??
            throw new ValidationException("Category was not found.");
    }

    private static void ValidateRequiredFieldValues(
        TaskTemplate? taskTemplate,
        IReadOnlyDictionary<Guid, JsonElement>? fieldValues)
    {
        if (taskTemplate is null)
        {
            return;
        }

        var providedFieldValues = fieldValues ?? new Dictionary<Guid, JsonElement>();

        foreach (var requiredField in taskTemplate.FieldDefinitions.Where(field =>
                     field.IsActive &&
                     field.Scope == FieldDefinitionScope.Header &&
                     field.IsRequired))
        {
            if (!providedFieldValues.TryGetValue(requiredField.Id, out var value) ||
                FieldValueIsEmpty(value))
            {
                throw new ValidationException(
                    $"Field '{requiredField.Label}' is required.");
            }
        }
    }

    private static void ApplyFieldValues(
        TaskItem taskItem,
        TaskTemplate? taskTemplate,
        IReadOnlyDictionary<Guid, JsonElement>? fieldValues,
        DateTimeOffset occurredAt)
    {
        if (fieldValues is null || fieldValues.Count == 0)
        {
            return;
        }

        if (taskTemplate is null)
        {
            throw new ValidationException(
                "A task template is required before field values can be updated.");
        }

        var definitions = taskTemplate.FieldDefinitions
            .Where(field => field.IsActive && field.Scope == FieldDefinitionScope.Header)
            .ToDictionary(field => field.Id);

        foreach (var (fieldDefinitionId, value) in fieldValues)
        {
            if (!definitions.TryGetValue(fieldDefinitionId, out var definition))
            {
                throw new ValidationException(
                    $"Field definition '{fieldDefinitionId}' was not found.");
            }

            taskItem.SetFieldValue(
                definition,
                NormalizeFieldValue(definition, value),
                occurredAt);
        }
    }

    private static void ValidateTimelineEntryHasContent(AddTaskTimelineEntryRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Note) ||
            FieldValuesContainMeaningfulContent(request.FieldValues))
        {
            return;
        }

        throw new ValidationException("Note text or entry fields are required.");
    }

    private static void ApplyTimelineEntryFieldValues(
        TaskItem taskItem,
        Guid entryId,
        TaskTemplate? taskTemplate,
        IReadOnlyDictionary<Guid, JsonElement>? fieldValues,
        DateTimeOffset occurredAt,
        bool requireRequiredFields)
    {
        if (fieldValues is null || fieldValues.Count == 0)
        {
            if (requireRequiredFields)
            {
                ValidateRequiredTimelineEntryFieldValues(taskTemplate, fieldValues);
            }

            return;
        }

        if (taskTemplate is null)
        {
            throw new ValidationException(
                "A task template is required before entry field values can be updated.");
        }

        var definitions = taskTemplate.FieldDefinitions
            .Where(field => field.IsActive && field.Scope == FieldDefinitionScope.Entry)
            .ToDictionary(field => field.Id);

        if (requireRequiredFields)
        {
            ValidateRequiredTimelineEntryFieldValues(taskTemplate, fieldValues);
        }

        foreach (var (fieldDefinitionId, value) in fieldValues)
        {
            if (!definitions.TryGetValue(fieldDefinitionId, out var definition))
            {
                throw new ValidationException(
                    $"Entry field definition '{fieldDefinitionId}' was not found.");
            }

            taskItem.SetTimelineEntryFieldValue(
                entryId,
                definition,
                NormalizeFieldValue(definition, value),
                occurredAt);
        }
    }

    private static void ValidateRequiredTimelineEntryFieldValues(
        TaskTemplate? taskTemplate,
        IReadOnlyDictionary<Guid, JsonElement>? fieldValues)
    {
        if (taskTemplate is null)
        {
            return;
        }

        var providedFieldValues = fieldValues ?? new Dictionary<Guid, JsonElement>();

        foreach (var requiredField in taskTemplate.FieldDefinitions.Where(field =>
                     field.IsActive &&
                     field.Scope == FieldDefinitionScope.Entry &&
                     field.IsRequired))
        {
            if (!providedFieldValues.TryGetValue(requiredField.Id, out var value) ||
                FieldValueIsEmpty(value))
            {
                throw new ValidationException(
                    $"Entry field '{requiredField.Label}' is required.");
            }
        }
    }

    private static void CopyFieldValues(
        TaskItem sourceTask,
        TaskItem copiedTask,
        IReadOnlyDictionary<Guid, FieldDefinition> fieldMap,
        DateTimeOffset occurredAt)
    {
        foreach (var fieldValue in sourceTask.FieldValues)
        {
            if (fieldMap.TryGetValue(fieldValue.FieldDefinitionId, out var definition) &&
                definition.Scope == FieldDefinitionScope.Header)
            {
                copiedTask.SetFieldValue(definition, fieldValue.ValueJson, occurredAt);
            }
        }
    }

    private static void CopyTimelineNotes(
        TaskItem sourceTask,
        TaskItem copiedTask,
        IReadOnlyDictionary<Guid, FieldDefinition>? fieldMap,
        DateTimeOffset occurredAt)
    {
        foreach (var entry in sourceTask.TimelineEntries
                     .Where(entry =>
                         entry.Kind == TaskTimelineEntryKind.NoteAdded &&
                         entry.DeletedAt is null &&
                         !string.IsNullOrWhiteSpace(entry.Details))
                     .OrderBy(entry => entry.OccurredAt))
        {
            var copiedEntry = copiedTask.AddNote(entry.Details!, occurredAt);

            if (fieldMap is null)
            {
                continue;
            }

            foreach (var fieldValue in entry.FieldValues)
            {
                if (fieldMap.TryGetValue(fieldValue.FieldDefinitionId, out var definition) &&
                    definition.Scope == FieldDefinitionScope.Entry)
                {
                    copiedTask.SetTimelineEntryFieldValue(
                        copiedEntry.Id,
                        definition,
                        fieldValue.ValueJson,
                        occurredAt);
                }
            }
        }
    }

    private static string NormalizeFieldValue(
        FieldDefinition fieldDefinition,
        JsonElement value)
    {
        if (FieldValueIsEmpty(value))
        {
            if (fieldDefinition.IsRequired)
            {
                throw new ValidationException(
                    $"Field '{fieldDefinition.Label}' is required.");
            }

            return "null";
        }

        return fieldDefinition.Type switch
        {
            FieldDefinitionType.Text or FieldDefinitionType.LongText =>
                NormalizeStringValue(fieldDefinition, value),
            FieldDefinitionType.Date => NormalizeDateValue(fieldDefinition, value),
            FieldDefinitionType.Checkbox => NormalizeCheckboxValue(fieldDefinition, value),
            FieldDefinitionType.Select => NormalizeSelectValue(fieldDefinition, value),
            _ => throw new ValidationException(
                $"Unsupported field type '{fieldDefinition.Type}'.")
        };
    }

    private static string NormalizeStringValue(
        FieldDefinition fieldDefinition,
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new ValidationException(
                $"Field '{fieldDefinition.Label}' requires a text value.");
        }

        return value.GetRawText();
    }

    private static string NormalizeDateValue(
        FieldDefinition fieldDefinition,
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new ValidationException(
                $"Field '{fieldDefinition.Label}' requires a date value.");
        }

        var dateText = value.GetString();

        if (string.IsNullOrWhiteSpace(dateText) ||
            (!DateOnly.TryParse(dateText, out _) &&
             !DateTimeOffset.TryParse(dateText, out _)))
        {
            throw new ValidationException(
                $"Field '{fieldDefinition.Label}' requires a valid date value.");
        }

        return value.GetRawText();
    }

    private static string NormalizeCheckboxValue(
        FieldDefinition fieldDefinition,
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.True &&
            value.ValueKind != JsonValueKind.False)
        {
            throw new ValidationException(
                $"Field '{fieldDefinition.Label}' requires a checkbox value.");
        }

        return value.GetRawText();
    }

    private static string NormalizeSelectValue(
        FieldDefinition fieldDefinition,
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new ValidationException(
                $"Field '{fieldDefinition.Label}' requires a selected option.");
        }

        var selectedOption = value.GetString()?.Trim();
        var options = TaskTemplateService.ParseOptions(fieldDefinition.OptionsJson);

        if (string.IsNullOrWhiteSpace(selectedOption) ||
            !options.Contains(selectedOption, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                $"Field '{fieldDefinition.Label}' must be one of the configured options.");
        }

        return JsonSerializer.Serialize(selectedOption);
    }

    private static bool FieldValueIsEmpty(JsonElement value)
    {
        return value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
            value.ValueKind == JsonValueKind.String &&
            string.IsNullOrWhiteSpace(value.GetString());
    }

    private static bool FieldValuesContainMeaningfulContent(
        IReadOnlyDictionary<Guid, JsonElement>? fieldValues)
    {
        if (fieldValues is null || fieldValues.Count == 0)
        {
            return false;
        }

        return fieldValues.Values.Any(value =>
            value.ValueKind == JsonValueKind.True ||
            value.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString()));
    }

    private async Task<TaskSyncStateResponse?> GetTaskSyncStateAsync(
        Guid workspaceId,
        Guid taskItemId,
        CancellationToken cancellationToken)
    {
        var states = await _syncService.ListTaskSyncStatesAsync(
            workspaceId,
            [taskItemId],
            cancellationToken);

        return states.GetValueOrDefault(taskItemId);
    }

    private static TaskItemSummaryResponse MapSummary(
        TaskItem taskItem,
        TaskSyncStateResponse? syncState = null,
        int subtaskCount = 0,
        TaskTemplate? taskTemplate = null)
    {
        var latestTimelineEntry = taskItem.TimelineEntries
            .Where(entry => entry.DeletedAt == null && entry.Kind == TaskTimelineEntryKind.NoteAdded)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .FirstOrDefault();
        var noteCount = CountNotes(taskItem);

        return new TaskItemSummaryResponse(
            taskItem.Id,
            taskItem.WorkspaceId,
            taskItem.ProjectId,
            taskItem.TaskTemplateId,
            taskItem.Title,
            taskItem.Status,
            taskItem.Category,
            taskItem.Color,
            taskItem.CreatedAt,
            taskItem.LastViewedAt,
            taskItem.LastTouchedAt,
            taskItem.FollowUpAt,
            taskItem.ArchivedAt,
            taskItem.ArchiveResolutionId,
            noteCount,
            MapShares(taskItem),
            syncState,
            latestTimelineEntry is null
                ? null
                : new TaskTimelineEntryResponse(
                    latestTimelineEntry.Id,
                    latestTimelineEntry.Kind.ToString(),
                    latestTimelineEntry.Summary,
                    latestTimelineEntry.Details,
                    latestTimelineEntry.OccurredAt,
                    latestTimelineEntry.UpdatedAt,
                    MapFieldValues(latestTimelineEntry.FieldValues)),
            taskItem.ParentTaskItemId,
            subtaskCount,
            MapBuiltInTemplateKind(taskTemplate),
            MapTodoEntries(taskItem, taskTemplate));
    }

    private static TaskItemDetailResponse MapDetail(
        TaskItem taskItem,
        TaskTemplate? taskTemplate,
        TaskSyncStateResponse? syncState = null,
        int subtaskCount = 0)
    {
        return new TaskItemDetailResponse(
            taskItem.Id,
            taskItem.WorkspaceId,
            taskItem.ProjectId,
            taskItem.TaskTemplateId,
            taskItem.Title,
            taskItem.Status,
            taskItem.Category,
            taskItem.Color,
            taskItem.CreatedAt,
            taskItem.LastViewedAt,
            taskItem.LastTouchedAt,
            taskItem.FollowUpAt,
            taskItem.ArchivedAt,
            taskItem.ArchiveResolutionId,
            CountNotes(taskItem),
            MapShares(taskItem),
            syncState,
            taskTemplate is null ? null : MapTaskTemplateForTask(taskTemplate, taskItem),
            taskItem.FieldValues
                .OrderBy(value => value.UpdatedAt)
                .ThenBy(value => value.Id)
                .Select(value => new FieldValueResponse(
                    value.Id,
                    value.FieldDefinitionId,
                    value.ValueJson,
                    value.UpdatedAt))
                .ToList(),
            taskItem.TimelineEntries
                .Where(entry => entry.DeletedAt == null)
                .OrderBy(entry => entry.OccurredAt)
                .ThenBy(entry => entry.Id)
                .Select(MapTimelineEntry)
                .ToList(),
            taskItem.ParentTaskItemId,
            subtaskCount,
            MapBuiltInTemplateKind(taskTemplate),
            MapTodoEntries(taskItem, taskTemplate));
    }

    private async Task<IReadOnlyDictionary<Guid, TaskTemplate>> LoadSummaryTemplatesAsync(
        IReadOnlyCollection<TaskItem> taskItems,
        CancellationToken cancellationToken)
    {
        var templateIds = taskItems
            .Where(taskItem => taskItem.TaskTemplateId.HasValue)
            .Select(taskItem => taskItem.TaskTemplateId!.Value)
            .Distinct()
            .ToArray();

        return await _taskTemplateRepository.ListByIdsAsync(templateIds, cancellationToken);
    }

    private static string? MapBuiltInTemplateKind(TaskTemplate? taskTemplate) =>
        taskTemplate is null || taskTemplate.BuiltInKind == TaskTemplateBuiltInKind.None
            ? null
            : taskTemplate.BuiltInKind.ToString();

    private static IReadOnlyList<TaskTodoEntryResponse>? MapTodoEntries(
        TaskItem taskItem,
        TaskTemplate? taskTemplate)
    {
        if (taskTemplate?.BuiltInKind != TaskTemplateBuiltInKind.Todo)
        {
            return null;
        }

        var itemField = taskTemplate.FieldDefinitions.FirstOrDefault(field =>
            field.IsActive &&
            field.Scope == FieldDefinitionScope.Entry &&
            string.Equals(field.Key, "item", StringComparison.OrdinalIgnoreCase));
        var doneField = taskTemplate.FieldDefinitions.FirstOrDefault(field =>
            field.IsActive &&
            field.Scope == FieldDefinitionScope.Entry &&
            field.Type == FieldDefinitionType.Checkbox &&
            string.Equals(field.Key, "done", StringComparison.OrdinalIgnoreCase));

        if (itemField is null || doneField is null)
        {
            return [];
        }

        return taskItem.TimelineEntries
            .Where(entry => entry.DeletedAt is null && entry.Kind == TaskTimelineEntryKind.NoteAdded)
            .OrderBy(entry => entry.OccurredAt)
            .ThenBy(entry => entry.Id)
            .Select(entry =>
            {
                var itemValue = entry.FieldValues.FirstOrDefault(value =>
                    value.FieldDefinitionId == itemField.Id)?.ValueJson;
                var doneValue = entry.FieldValues.FirstOrDefault(value =>
                    value.FieldDefinitionId == doneField.Id)?.ValueJson;

                return new TaskTodoEntryResponse(
                    entry.Id,
                    ParseJsonString(itemValue) ?? entry.Details ?? string.Empty,
                    string.Equals(doneValue, "true", StringComparison.OrdinalIgnoreCase),
                    doneField.Id);
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Label))
            .ToList();
    }

    private static string? ParseJsonString(string? valueJson)
    {
        if (string.IsNullOrWhiteSpace(valueJson) || valueJson == "null")
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(valueJson);
            return document.RootElement.ValueKind == JsonValueKind.String
                ? document.RootElement.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TaskTimelineEntryResponse MapTimelineEntry(TaskTimelineEntry entry)
    {
        return new TaskTimelineEntryResponse(
            entry.Id,
            entry.Kind.ToString(),
            entry.Summary,
            entry.Details,
            entry.OccurredAt,
            entry.UpdatedAt,
            MapFieldValues(entry.FieldValues));
    }

    private static IReadOnlyList<FieldValueResponse> MapFieldValues(
        IEnumerable<TaskTimelineEntryFieldValue> fieldValues)
    {
        return fieldValues
            .OrderBy(value => value.UpdatedAt)
            .ThenBy(value => value.Id)
            .Select(value => new FieldValueResponse(
                value.Id,
                value.FieldDefinitionId,
                value.ValueJson,
                value.UpdatedAt))
            .ToList();
    }

    private static IReadOnlyList<TaskItemShareResponse> MapShares(TaskItem taskItem)
    {
        return taskItem.Shares
            .Where(share => share.RevokedAt is null)
            .OrderBy(share => share.Email)
            .Select(MapShare)
            .ToList();
    }

    private static TaskItemShareResponse MapShare(TaskItemShare share)
    {
        return new TaskItemShareResponse(
            share.Id,
            share.Email,
            share.SharedWithUserId,
            share.SharedByUserId,
            share.Role,
            share.CreatedAt,
            share.ExpiresAt,
            share.AcceptedAt,
            share.RevokedAt);
    }

    private static TaskShareInboxResponse MapIncomingShare(TaskShareInboxItem item)
    {
        return new TaskShareInboxResponse(
            item.Share.Id,
            item.TaskItem.Id,
            item.Workspace.Id,
            item.Workspace.Name,
            item.Workspace.Color,
            item.TaskItem.Title,
            item.SharedByUser.Email,
            item.SharedByUser.DisplayName,
            item.Share.Role,
            item.Share.CreatedAt,
            item.Share.ExpiresAt,
            item.Share.AcceptedAt);
    }

    private static int CountNotes(TaskItem taskItem)
    {
        return taskItem.TimelineEntries.Count(entry =>
            entry.Kind == TaskTimelineEntryKind.NoteAdded &&
            entry.DeletedAt == null);
    }

    private static TaskTemplateDetailResponse MapTaskTemplateForTask(
        TaskTemplate taskTemplate,
        TaskItem taskItem)
    {
        var fieldValueDefinitionIds = GetTaskFieldValueDefinitionIds(taskItem);

        return new TaskTemplateDetailResponse(
            taskTemplate.Id,
            taskTemplate.Name,
            taskTemplate.CreatedAt,
            taskTemplate.UpdatedAt,
            TaskTemplateService.MapLayout(taskTemplate, taskTemplate.FieldDefinitions
                .Where(field => field.IsActive || fieldValueDefinitionIds.Contains(field.Id))),
            taskTemplate.FieldDefinitions
                .Where(field => field.IsActive || fieldValueDefinitionIds.Contains(field.Id))
                .OrderBy(field => field.Scope)
                .ThenBy(field => field.SortOrder)
                .ThenBy(field => field.Label)
                .Select(TaskTemplateService.MapField)
                .ToList(),
            taskTemplate.BuiltInKind == TaskTemplateBuiltInKind.None
                ? null
                : taskTemplate.BuiltInKind.ToString(),
            taskTemplate.IsProtected);
    }

    private static IReadOnlySet<Guid> GetTaskFieldValueDefinitionIds(TaskItem taskItem)
    {
        return taskItem.FieldValues
            .Select(value => value.FieldDefinitionId)
            .Concat(taskItem.TimelineEntries
                .SelectMany(entry => entry.FieldValues)
                .Select(value => value.FieldDefinitionId))
            .ToHashSet();
    }

    private sealed record CopiedTemplate(
        TaskTemplate Template,
        IReadOnlyDictionary<Guid, FieldDefinition> FieldMap);
}
