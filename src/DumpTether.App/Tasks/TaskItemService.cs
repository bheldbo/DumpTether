using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using DumpTether.App.Templates;
using DumpTether.App.Views;
using DumpTether.Domain;

namespace DumpTether.App.Tasks;

internal sealed class TaskItemService : ITaskItemService
{
    private readonly IClock _clock;
    private readonly IDevelopmentWorkspaceProvider _developmentWorkspaceProvider;
    private readonly ISavedViewRepository _savedViewRepository;
    private readonly ITaskItemRepository _taskItemRepository;

    public TaskItemService(
        IClock clock,
        IDevelopmentWorkspaceProvider developmentWorkspaceProvider,
        ISavedViewRepository savedViewRepository,
        ITaskItemRepository taskItemRepository)
    {
        _clock = clock;
        _developmentWorkspaceProvider = developmentWorkspaceProvider;
        _savedViewRepository = savedViewRepository;
        _taskItemRepository = taskItemRepository;
    }

    public async Task<TaskItemDetailResponse> CreateAsync(
        CreateTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var now = _clock.UtcNow;
        var taskTemplate = await ResolveTaskTemplateForCreateAsync(
            context.WorkspaceId,
            request.TaskTemplateId,
            cancellationToken);
        var taskItem = TaskItem.Create(
            context.WorkspaceId,
            context.ProjectId,
            request.Title,
            now,
            taskTemplate?.Id);

        ValidateRequiredFieldValues(taskTemplate, request.FieldValues);
        ApplyFieldValues(taskItem, taskTemplate, request.FieldValues, now);

        await _taskItemRepository.AddAsync(taskItem, cancellationToken);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);

        return MapDetail(taskItem, taskTemplate);
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
        var query = await BuildQueryAsync(context.WorkspaceId, request, cancellationToken);
        var taskItems = await _taskItemRepository.ListAsync(
            query,
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
            projectId: null,
            trackChanges: true,
            cancellationToken);

        if (taskItem is null)
        {
            return null;
        }

        taskItem.MarkViewed(_clock.UtcNow);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);

        var taskTemplate = await ResolveTaskTemplateForDetailAsync(
            taskItem,
            includeDeleted: true,
            cancellationToken);

        return MapDetail(taskItem, taskTemplate);
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
            projectId: null,
            trackChanges: true,
            cancellationToken);

        if (taskItem is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var taskTemplate = await ResolveTaskTemplateForDetailAsync(
            taskItem,
            includeDeleted: true,
            cancellationToken);

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

        ApplyFieldValues(taskItem, taskTemplate, request.FieldValues, now);
        await _taskItemRepository.SaveChangesAsync(cancellationToken);

        return MapDetail(taskItem, taskTemplate);
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

        var taskTemplate = await ResolveTaskTemplateForDetailAsync(
            taskItem,
            includeDeleted: true,
            cancellationToken);

        return MapDetail(taskItem, taskTemplate);
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
            projectId: null,
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

        var taskTemplate = await ResolveTaskTemplateForDetailAsync(
            taskItem,
            includeDeleted: true,
            cancellationToken);

        return MapDetail(taskItem, taskTemplate);
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

        var taskTemplate = await ResolveTaskTemplateForDetailAsync(
            taskItem,
            includeDeleted: true,
            cancellationToken);

        return MapDetail(taskItem, taskTemplate);
    }

    private async Task<TaskItem?> GetTaskItemForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);

        return await _taskItemRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            projectId: null,
            trackChanges: true,
            cancellationToken);
    }

    private async Task<TaskItemQuery> BuildQueryAsync(
        Guid workspaceId,
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

        return new TaskItemQuery(
            workspaceId,
            filter.ProjectId == Guid.Empty ? null : filter.ProjectId,
            filter.Status,
            ParseArchiveFilter(filter.Archive),
            ParseFollowUpFilter(filter.FollowUp),
            filter.NotViewedSinceDays,
            filter.NotTouchedSinceDays,
            filter.Text,
            ParseSortField(sort.Field),
            string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase),
            _clock.UtcNow);
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

    private async Task<TaskTemplate?> ResolveTaskTemplateForCreateAsync(
        Guid workspaceId,
        Guid? requestedTemplateId,
        CancellationToken cancellationToken)
    {
        if (requestedTemplateId.HasValue && requestedTemplateId.Value != Guid.Empty)
        {
            var requestedTemplate = await _taskItemRepository.GetTaskTemplateByIdAsync(
                requestedTemplateId.Value,
                workspaceId,
                includeDeleted: false,
                cancellationToken);

            return requestedTemplate ??
                throw new ValidationException("Task template was not found.");
        }

        return await _taskItemRepository.GetDefaultTaskTemplateAsync(
            workspaceId,
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
            taskItem.WorkspaceId,
            includeDeleted,
            cancellationToken);
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
            .Where(field => field.IsActive)
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

    private static TaskItemSummaryResponse MapSummary(TaskItem taskItem)
    {
        var latestTimelineEntry = taskItem.TimelineEntries
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .FirstOrDefault();

        return new TaskItemSummaryResponse(
            taskItem.Id,
            taskItem.WorkspaceId,
            taskItem.ProjectId,
            taskItem.TaskTemplateId,
            taskItem.Title,
            taskItem.Status,
            taskItem.CreatedAt,
            taskItem.LastViewedAt,
            taskItem.LastTouchedAt,
            taskItem.FollowUpAt,
            taskItem.ArchivedAt,
            taskItem.ArchiveResolutionId,
            latestTimelineEntry is null
                ? null
                : new TaskTimelineEntryResponse(
                    latestTimelineEntry.Id,
                    latestTimelineEntry.Kind.ToString(),
                    latestTimelineEntry.Summary,
                    latestTimelineEntry.Details,
                    latestTimelineEntry.OccurredAt));
    }

    private static TaskItemDetailResponse MapDetail(
        TaskItem taskItem,
        TaskTemplate? taskTemplate)
    {
        return new TaskItemDetailResponse(
            taskItem.Id,
            taskItem.WorkspaceId,
            taskItem.ProjectId,
            taskItem.TaskTemplateId,
            taskItem.Title,
            taskItem.Status,
            taskItem.CreatedAt,
            taskItem.LastViewedAt,
            taskItem.LastTouchedAt,
            taskItem.FollowUpAt,
            taskItem.ArchivedAt,
            taskItem.ArchiveResolutionId,
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

    private static TaskTemplateDetailResponse MapTaskTemplateForTask(
        TaskTemplate taskTemplate,
        TaskItem taskItem)
    {
        var fieldValueDefinitionIds = taskItem.FieldValues
            .Select(value => value.FieldDefinitionId)
            .ToHashSet();

        return new TaskTemplateDetailResponse(
            taskTemplate.Id,
            taskTemplate.Name,
            taskTemplate.CreatedAt,
            taskTemplate.UpdatedAt,
            taskTemplate.FieldDefinitions
                .Where(field => field.IsActive || fieldValueDefinitionIds.Contains(field.Id))
                .OrderBy(field => field.SortOrder)
                .ThenBy(field => field.Label)
                .Select(TaskTemplateService.MapField)
                .ToList());
    }
}
