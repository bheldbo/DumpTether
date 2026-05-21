using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using DumpTether.App.Templates;
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

        if (taskItem is null)
        {
            return null;
        }

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
            context.ProjectId,
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
            context.ProjectId,
            trackChanges: true,
            cancellationToken);
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
            taskItem.ArchiveResolutionId);
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
