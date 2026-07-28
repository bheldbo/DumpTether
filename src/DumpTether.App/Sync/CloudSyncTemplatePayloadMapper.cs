using System.Text.Json;
using DumpTether.App.Templates;
using DumpTether.App.Tasks;
using DumpTether.Domain;

namespace DumpTether.App.Sync;

internal static class CloudSyncTemplatePayloadMapper
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public static RemoteTaskTemplateProjection CreateProjection(
        CloudSyncTaskTemplateResponse remoteTemplate,
        TaskTemplate localTemplate)
    {
        var remoteFieldsByKey = remoteTemplate.Fields
            .ToDictionary(
                field => new TemplateFieldKey(field.Scope, field.Key),
                field => field.Id);
        var localToRemoteFieldIds = localTemplate.FieldDefinitions
            .Where(field => field.IsActive)
            .Where(field => remoteFieldsByKey.ContainsKey(CreateKey(field)))
            .ToDictionary(
                field => field.Id,
                field => remoteFieldsByKey[CreateKey(field)]);

        return new RemoteTaskTemplateProjection(
            remoteTemplate.Id,
            localToRemoteFieldIds,
            localTemplate);
    }

    public static CloudSyncCreateTaskTemplateRequest CreateRequest(
        TaskTemplate localTemplate,
        CloudSyncTaskTemplateResponse? remoteTemplate = null)
    {
        return new CloudSyncCreateTaskTemplateRequest(
            localTemplate.Name,
            CreateFieldRequests(localTemplate, remoteTemplate),
            CreateLayoutRequest(localTemplate));
    }

    public static CloudSyncUpdateTaskTemplateRequest UpdateRequest(
        TaskTemplate localTemplate,
        CloudSyncTaskTemplateResponse remoteTemplate)
    {
        return new CloudSyncUpdateTaskTemplateRequest(
            localTemplate.Name,
            CreateFieldRequests(localTemplate, remoteTemplate),
            CreateLayoutRequest(localTemplate));
    }

    public static bool TemplateDiffers(
        TaskTemplate localTemplate,
        CloudSyncTaskTemplateResponse remoteTemplate)
    {
        if (!string.Equals(localTemplate.Name, remoteTemplate.Name, StringComparison.Ordinal))
        {
            return true;
        }

        var localFields = CreateFieldRequests(localTemplate, remoteTemplate)
            .Select(field => new
            {
                field.Name,
                field.Type,
                field.Scope,
                field.Required,
                field.SortOrder,
                Options = string.Join('\u001f', field.Options),
                field.LayoutRow,
                field.LayoutColumn,
                field.LayoutRowSpan,
                field.LayoutColumnSpan,
                field.LayoutWeight
            });
        var remoteFields = remoteTemplate.Fields
            .Select(field => new
            {
                field.Name,
                field.Type,
                field.Scope,
                field.Required,
                field.SortOrder,
                Options = string.Join('\u001f', field.Options),
                field.LayoutRow,
                field.LayoutColumn,
                field.LayoutRowSpan,
                field.LayoutColumnSpan,
                field.LayoutWeight
            });

        return !localFields.SequenceEqual(remoteFields);
    }

    public static IReadOnlyDictionary<Guid, string>? BuildRemoteFieldValuePayload(
        TaskItem localTask,
        IReadOnlyDictionary<Guid, Guid> localToRemoteFieldIds)
    {
        if (localTask.FieldValues.Count == 0 || localToRemoteFieldIds.Count == 0)
        {
            return null;
        }

        var values = new Dictionary<Guid, string>();
        foreach (var fieldValue in localTask.FieldValues)
        {
            if (localToRemoteFieldIds.TryGetValue(
                    fieldValue.FieldDefinitionId,
                    out var remoteFieldDefinitionId))
            {
                values[remoteFieldDefinitionId] = fieldValue.ValueJson;
            }
        }

        return values.Count == 0 ? null : values;
    }

    public static IReadOnlyDictionary<Guid, string>? BuildRemoteFieldValuePayload(
        TaskTimelineEntry localEntry,
        IReadOnlyDictionary<Guid, Guid> localToRemoteFieldIds)
    {
        if (localEntry.FieldValues.Count == 0 || localToRemoteFieldIds.Count == 0)
        {
            return null;
        }

        var values = new Dictionary<Guid, string>();
        foreach (var fieldValue in localEntry.FieldValues)
        {
            if (localToRemoteFieldIds.TryGetValue(
                    fieldValue.FieldDefinitionId,
                    out var remoteFieldDefinitionId))
            {
                values[remoteFieldDefinitionId] = fieldValue.ValueJson;
            }
        }

        return values.Count == 0 ? null : values;
    }

    public static TaskTemplate CreateLocalTemplate(
        CloudSyncTaskTemplateResponse remoteTemplate,
        Guid ownerUserId,
        DateTimeOffset createdAt)
    {
        var localTemplate = TaskTemplate.Create(ownerUserId, remoteTemplate.Name, createdAt);
        localTemplate.UpdateLayout(
            JsonSerializer.Serialize(remoteTemplate.Layout.Header, JsonSerializerOptions),
            JsonSerializer.Serialize(remoteTemplate.Layout.Entry, JsonSerializerOptions),
            createdAt);

        foreach (var field in remoteTemplate.Fields
                     .OrderBy(field => field.Scope)
                     .ThenBy(field => field.SortOrder)
                     .ThenBy(field => field.Name))
        {
            localTemplate.AddFieldDefinition(
                field.Key,
                field.Name,
                ParseFieldType(field.Type),
                ParseFieldScope(field.Scope),
                field.Required,
                field.SortOrder,
                field.Options.Count == 0
                    ? null
                    : JsonSerializer.Serialize(field.Options, JsonSerializerOptions),
                field.LayoutRow,
                field.LayoutColumn,
                field.LayoutRowSpan,
                field.LayoutColumnSpan,
                field.LayoutWeight);
        }

        return localTemplate;
    }

    public static RemoteToLocalTaskTemplateProjection CreateRemoteToLocalProjection(
        CloudSyncTaskTemplateResponse remoteTemplate,
        TaskTemplate localTemplate)
    {
        var localFieldsByKey = localTemplate.FieldDefinitions
            .Where(field => field.IsActive)
            .ToDictionary(
                CreateKey,
                field => field.Id);
        var remoteToLocalFieldIds = remoteTemplate.Fields
            .Where(field => localFieldsByKey.ContainsKey(new TemplateFieldKey(field.Scope, field.Key)))
            .ToDictionary(
                field => field.Id,
                field => localFieldsByKey[new TemplateFieldKey(field.Scope, field.Key)]);

        return new RemoteToLocalTaskTemplateProjection(
            localTemplate.Id,
            remoteToLocalFieldIds,
            localTemplate);
    }

    public static IReadOnlyDictionary<Guid, string>? BuildLocalFieldValuePayload(
        CloudSyncTaskResponse remoteTask,
        IReadOnlyDictionary<Guid, Guid> remoteToLocalFieldIds)
    {
        if (remoteTask.FieldValues is null ||
            remoteTask.FieldValues.Count == 0 ||
            remoteToLocalFieldIds.Count == 0)
        {
            return null;
        }

        var values = new Dictionary<Guid, string>();
        foreach (var fieldValue in remoteTask.FieldValues)
        {
            if (!remoteToLocalFieldIds.TryGetValue(
                    fieldValue.FieldDefinitionId,
                    out var localFieldDefinitionId))
            {
                continue;
            }

            using var document = JsonDocument.Parse(fieldValue.ValueJson);
            values[localFieldDefinitionId] = document.RootElement.GetRawText();
        }

        return values.Count == 0 ? null : values;
    }

    public static IReadOnlyDictionary<Guid, string>? BuildLocalFieldValuePayload(
        CloudSyncTimelineEntryResponse remoteEntry,
        IReadOnlyDictionary<Guid, Guid> remoteToLocalFieldIds)
    {
        if (remoteEntry.FieldValues is null ||
            remoteEntry.FieldValues.Count == 0 ||
            remoteToLocalFieldIds.Count == 0)
        {
            return null;
        }

        var values = new Dictionary<Guid, string>();
        foreach (var fieldValue in remoteEntry.FieldValues)
        {
            if (!remoteToLocalFieldIds.TryGetValue(
                    fieldValue.FieldDefinitionId,
                    out var localFieldDefinitionId))
            {
                continue;
            }

            using var document = JsonDocument.Parse(fieldValue.ValueJson);
            values[localFieldDefinitionId] = document.RootElement.GetRawText();
        }

        return values.Count == 0 ? null : values;
    }

    private static IReadOnlyList<CloudSyncUpsertFieldDefinitionRequest> CreateFieldRequests(
        TaskTemplate localTemplate,
        CloudSyncTaskTemplateResponse? remoteTemplate)
    {
        var remoteFieldsByKey = (remoteTemplate?.Fields ?? [])
            .ToDictionary(
                field => new TemplateFieldKey(field.Scope, field.Key),
                field => field.Id);

        return localTemplate.FieldDefinitions
            .Where(field => field.IsActive)
            .OrderBy(field => field.Scope)
            .ThenBy(field => field.SortOrder)
            .ThenBy(field => field.Label)
            .Select(field =>
            {
                remoteFieldsByKey.TryGetValue(
                    new TemplateFieldKey(field.Scope.ToString(), field.Key),
                    out var remoteFieldId);

                return new CloudSyncUpsertFieldDefinitionRequest(
                    remoteFieldId == Guid.Empty ? null : remoteFieldId,
                    field.Label,
                    field.Type.ToString(),
                    field.Scope.ToString(),
                    field.IsRequired,
                    field.SortOrder,
                    TaskTemplateService.ParseOptions(field.OptionsJson),
                    field.LayoutRow,
                    field.LayoutColumn,
                    field.LayoutRowSpan,
                    field.LayoutColumnSpan,
                    field.LayoutWeight);
            })
            .ToList();
    }

    private static CloudSyncTaskTemplateLayoutRequest CreateLayoutRequest(
        TaskTemplate localTemplate)
    {
        var layout = TaskTemplateService.MapLayout(
            localTemplate,
            localTemplate.FieldDefinitions.Where(field => field.IsActive));

        return new CloudSyncTaskTemplateLayoutRequest(
            layout.Header
                .Select(row => new CloudSyncTaskTemplateLayoutRowRequest(
                    row.Row,
                    row.ColumnWeights,
                    row.Height))
                .ToList(),
            layout.Entry
                .Select(row => new CloudSyncTaskTemplateLayoutRowRequest(
                    row.Row,
                    row.ColumnWeights,
                    row.Height))
                .ToList());
    }

    private static FieldDefinitionType ParseFieldType(string value)
    {
        return Enum.TryParse<FieldDefinitionType>(
                value,
                ignoreCase: true,
                out var type) &&
            Enum.IsDefined(type)
            ? type
            : throw new InvalidOperationException($"Cloud template field type '{value}' is not supported.");
    }

    private static FieldDefinitionScope ParseFieldScope(string value)
    {
        return Enum.TryParse<FieldDefinitionScope>(
                value,
                ignoreCase: true,
                out var scope) &&
            Enum.IsDefined(scope)
            ? scope
            : throw new InvalidOperationException($"Cloud template field scope '{value}' is not supported.");
    }

    private static TemplateFieldKey CreateKey(FieldDefinition field)
    {
        return new TemplateFieldKey(field.Scope.ToString(), field.Key);
    }

    private sealed record TemplateFieldKey(string Scope, string Key);
}

internal sealed record RemoteTaskTemplateProjection(
    Guid? RemoteTemplateId,
    IReadOnlyDictionary<Guid, Guid> LocalToRemoteFieldIds,
    TaskTemplate? LocalTemplate)
{
    public static readonly RemoteTaskTemplateProjection Empty = new(null, new Dictionary<Guid, Guid>(), null);
}

internal sealed record RemoteToLocalTaskTemplateProjection(
    Guid? LocalTemplateId,
    IReadOnlyDictionary<Guid, Guid> RemoteToLocalFieldIds,
    TaskTemplate? LocalTemplate)
{
    public static readonly RemoteToLocalTaskTemplateProjection Empty = new(
        null,
        new Dictionary<Guid, Guid>(),
        null);
}
