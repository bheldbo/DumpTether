using System.Text.Json;
using DumpTether.App.Tasks;
using DumpTether.Domain;

namespace DumpTether.App.Templates;

internal sealed class BuiltInTaskTemplateProvisioner : IBuiltInTaskTemplateProvisioner
{
    public const string BasicName = "Basic Task";

    private const double HeaderHeight = 190;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IClock _clock;
    private readonly ITaskTemplateRepository _repository;

    public BuiltInTaskTemplateProvisioner(IClock clock, ITaskTemplateRepository repository)
    {
        _clock = clock;
        _repository = repository;
    }

    public async Task EnsureAsync(Guid? ownerUserId, CancellationToken cancellationToken)
    {
        var templates = await _repository.ListAsync(ownerUserId, cancellationToken);
        await EnsureBasicAsync(templates, ownerUserId, cancellationToken);
        await RetireLegacyTodoAsync(templates, ownerUserId, cancellationToken);
    }

    private async Task EnsureBasicAsync(
        IReadOnlyList<TaskTemplate> templates,
        Guid? ownerUserId,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var headerLayout = SerializeLayout([new TaskTemplateLayoutRowResponse(1, [1], HeaderHeight)]);
        var entryLayout = SerializeLayout([]);
        var templateSummary = templates.FirstOrDefault(candidate =>
            candidate.BuiltInKind == TaskTemplateBuiltInKind.Basic) ??
            templates.FirstOrDefault(candidate =>
                candidate.BuiltInKind == TaskTemplateBuiltInKind.None &&
                string.Equals(candidate.Name, BasicName, StringComparison.OrdinalIgnoreCase));
        var template = templateSummary is null
            ? null
            : await _repository.GetByIdAsync(
                templateSummary.Id,
                ownerUserId,
                trackChanges: true,
                includeDeleted: true,
                cancellationToken);

        if (template is null)
        {
            template = TaskTemplate.Create(ownerUserId, BasicName, now);
            await _repository.AddAsync(template, cancellationToken);
        }

        var activeFields = template.FieldDefinitions.Where(field => field.IsActive).ToList();
        var descriptionField = activeFields.FirstOrDefault(field =>
            field.Type == FieldDefinitionType.LongText &&
            field.Scope == FieldDefinitionScope.Header);

        if (template.BuiltInKind == TaskTemplateBuiltInKind.Basic &&
            template.Name == BasicName &&
            template.HeaderLayoutJson == headerLayout &&
            template.EntryLayoutJson == entryLayout &&
            activeFields.Count == 1 &&
            descriptionField is not null &&
            FieldMatches(
                descriptionField, "description", "Description", FieldDefinitionType.LongText,
                FieldDefinitionScope.Header, false, 0, 1, 1, 1))
        {
            return;
        }

        template.RestoreBuiltInDefinition(
            TaskTemplateBuiltInKind.Basic,
            BasicName,
            headerLayout,
            entryLayout,
            now);

        descriptionField ??= template.AddFieldDefinition(
            "description", "Description", FieldDefinitionType.LongText, FieldDefinitionScope.Header,
            isRequired: false, sortOrder: 0, layoutRow: 1, layoutColumn: 1, layoutWeight: 1);
        descriptionField.Update(
            "description", "Description", FieldDefinitionType.LongText,
            FieldDefinitionScope.Header, false, 0, null, 1, 1, 1, 1, 1);

        foreach (var obsoleteField in activeFields.Where(field => field.Id != descriptionField.Id))
        {
            obsoleteField.Deactivate(now);
        }
    }

    private async Task RetireLegacyTodoAsync(
        IReadOnlyList<TaskTemplate> templates,
        Guid? ownerUserId,
        CancellationToken cancellationToken)
    {
        var legacyTemplate = templates.FirstOrDefault(candidate =>
            candidate.BuiltInKind == TaskTemplateBuiltInKind.Todo);

        if (legacyTemplate is null)
        {
            return;
        }

        var trackedTemplate = await _repository.GetByIdAsync(
            legacyTemplate.Id,
            ownerUserId,
            trackChanges: true,
            includeDeleted: true,
            cancellationToken);

        if (trackedTemplate is null)
        {
            return;
        }

        trackedTemplate.RetireBuiltIn(TaskTemplateBuiltInKind.Todo, _clock.UtcNow);
    }

    private static string SerializeLayout(IReadOnlyList<TaskTemplateLayoutRowResponse> rows) =>
        JsonSerializer.Serialize(rows, JsonOptions);

    private static bool FieldMatches(
        FieldDefinition field,
        string key,
        string label,
        FieldDefinitionType type,
        FieldDefinitionScope scope,
        bool isRequired,
        int sortOrder,
        int layoutRow,
        int layoutColumn,
        double layoutWeight)
    {
        return field.Key == key &&
            field.Label == label &&
            field.Type == type &&
            field.Scope == scope &&
            field.IsRequired == isRequired &&
            field.SortOrder == sortOrder &&
            field.OptionsJson is null &&
            field.LayoutRow == layoutRow &&
            field.LayoutColumn == layoutColumn &&
            field.LayoutRowSpan == 1 &&
            field.LayoutColumnSpan == 1 &&
            field.LayoutWeight == layoutWeight;
    }
}
