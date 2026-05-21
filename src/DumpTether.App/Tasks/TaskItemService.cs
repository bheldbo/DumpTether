using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using DumpTether.Domain;

namespace DumpTether.App.Tasks;

internal sealed class TaskItemService : ITaskItemService
{
    private readonly IClock _clock;
    private readonly IDevelopmentWorkspaceProvider _developmentWorkspaceProvider;
    private readonly ITaskItemRepository _taskItemRepository;

    public TaskItemService(
        IClock clock,
        IDevelopmentWorkspaceProvider developmentWorkspaceProvider,
        ITaskItemRepository taskItemRepository)
    {
        _clock = clock;
        _developmentWorkspaceProvider = developmentWorkspaceProvider;
        _taskItemRepository = taskItemRepository;
    }

    public async Task<TaskItemDetailResponse> CreateAsync(
        CreateTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var now = _clock.UtcNow;
        var taskItem = TaskItem.Create(context.WorkspaceId, context.ProjectId, request.Title, now);

        await ApplyFieldValuesAsync(taskItem, request.FieldValues, now, cancellationToken);

        await _taskItemRepository.AddAsync(taskItem, cancellationToken);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);

        return MapDetail(taskItem);
    }

    public async Task<IReadOnlyList<TaskItemSummaryResponse>> ListAsync(
        TaskItemListScope scope,
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var taskItems = await _taskItemRepository.ListAsync(
            context.WorkspaceId,
            context.ProjectId,
            scope,
            cancellationToken);

        return taskItems.Select(MapSummary).ToList();
    }

    public async Task<TaskItemDetailResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var taskItem = await _taskItemRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            context.ProjectId,
            trackChanges: false,
            cancellationToken);

        return taskItem is null ? null : MapDetail(taskItem);
    }

    public async Task<TaskItemDetailResponse?> UpdateAsync(
        Guid id,
        UpdateTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var taskItem = await _taskItemRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            context.ProjectId,
            trackChanges: true,
            cancellationToken);

        if (taskItem is null)
        {
            return null;
        }

        var now = _clock.UtcNow;

        if (request.Title is not null)
        {
            taskItem.Rename(request.Title, now);
        }

        if (request.Status is not null)
        {
            taskItem.ChangeStatus(request.Status, now);
        }

        if (request.FollowUpAt.HasValue)
        {
            taskItem.SetFollowUp(request.FollowUpAt.Value, now);
        }

        await ApplyFieldValuesAsync(taskItem, request.FieldValues, now, cancellationToken);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);

        return MapDetail(taskItem);
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

        taskItem.AddNote(request.Note, _clock.UtcNow);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);

        return MapDetail(taskItem);
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
        var taskItem = await _taskItemRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            context.ProjectId,
            trackChanges: true,
            cancellationToken);

        if (taskItem is null)
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

        return MapDetail(taskItem);
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

        return MapDetail(taskItem);
    }

    private async Task<TaskItem?> GetTaskItemForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);

        return await _taskItemRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            context.ProjectId,
            trackChanges: true,
            cancellationToken);
    }

    private async Task ApplyFieldValuesAsync(
        TaskItem taskItem,
        IReadOnlyDictionary<Guid, JsonElement>? fieldValues,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        if (fieldValues is null || fieldValues.Count == 0)
        {
            return;
        }

        var definitions = await _taskItemRepository.GetFieldDefinitionsAsync(
            fieldValues.Keys,
            cancellationToken);

        foreach (var (fieldDefinitionId, value) in fieldValues)
        {
            if (!definitions.TryGetValue(fieldDefinitionId, out var definition))
            {
                throw new ValidationException(
                    $"Field definition '{fieldDefinitionId}' was not found.");
            }

            taskItem.SetFieldValue(definition, value.GetRawText(), occurredAt);
        }
    }

    private static TaskItemSummaryResponse MapSummary(TaskItem taskItem)
    {
        return new TaskItemSummaryResponse(
            taskItem.Id,
            taskItem.WorkspaceId,
            taskItem.ProjectId,
            taskItem.Title,
            taskItem.Status,
            taskItem.CreatedAt,
            taskItem.LastViewedAt,
            taskItem.LastTouchedAt,
            taskItem.FollowUpAt,
            taskItem.ArchivedAt,
            taskItem.ArchiveResolutionId);
    }

    private static TaskItemDetailResponse MapDetail(TaskItem taskItem)
    {
        return new TaskItemDetailResponse(
            taskItem.Id,
            taskItem.WorkspaceId,
            taskItem.ProjectId,
            taskItem.Title,
            taskItem.Status,
            taskItem.CreatedAt,
            taskItem.LastViewedAt,
            taskItem.LastTouchedAt,
            taskItem.FollowUpAt,
            taskItem.ArchivedAt,
            taskItem.ArchiveResolutionId,
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
                .OrderBy(entry => entry.OccurredAt)
                .ThenBy(entry => entry.Id)
                .Select(entry => new TaskTimelineEntryResponse(
                    entry.Id,
                    entry.Kind.ToString(),
                    entry.Summary,
                    entry.Details,
                    entry.OccurredAt))
                .ToList());
    }
}
