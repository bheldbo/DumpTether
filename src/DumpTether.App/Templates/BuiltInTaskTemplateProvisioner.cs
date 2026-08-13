using System.Text.Json;
using DumpTether.App.Tasks;
using DumpTether.Domain;

namespace DumpTether.App.Templates;

internal sealed class BuiltInTaskTemplateProvisioner : IBuiltInTaskTemplateProvisioner
{
    public const string BasicName = "Basic Task";
    public const string TodoName = "ToDo Task";

    private const double HeaderHeight = 190;
    private const double EntryHeight = 90;
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
        await EnsureTodoAsync(templates, ownerUserId, cancellationToken);
    }

    private async Task EnsureBasicAsync(
        IReadOnlyList<TaskTemplate> templates,
        Guid? ownerUserId,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var headerLayout = SerializeLayout([new TaskTemplateLayoutRowResponse(1, [1], HeaderHeight)]);
        var entryLayout = SerializeLayout([new TaskTemplateLayoutRowResponse(1, [1], EntryHeight)]);
        var template = templates.FirstOrDefault(candidate =>
            candidate.BuiltInKind == TaskTemplateBuiltInKind.Basic) ??
            templates.FirstOrDefault(candidate =>
                candidate.BuiltInKind == TaskTemplateBuiltInKind.None &&
                string.Equals(candidate.Name, BasicName, StringComparison.OrdinalIgnoreCase));

        if (template is null)
        {
            template = TaskTemplate.Create(ownerUserId, BasicName, now);
            await _repository.AddAsync(template, cancellationToken);
        }

        var activeFields = template.FieldDefinitions.Where(field => field.IsActive).ToList();
        var contextField = activeFields.FirstOrDefault(field =>
            field.Type == FieldDefinitionType.LongText &&
            field.Scope == FieldDefinitionScope.Header);

        if (template.BuiltInKind == TaskTemplateBuiltInKind.Basic &&
            template.Name == BasicName &&
            template.HeaderLayoutJson == headerLayout &&
            template.EntryLayoutJson == entryLayout &&
            activeFields.Count == 1 &&
            contextField is not null &&
            FieldMatches(
                contextField, "context", "Context", FieldDefinitionType.LongText,
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

        contextField ??= template.AddFieldDefinition(
            "context", "Context", FieldDefinitionType.LongText, FieldDefinitionScope.Header,
            isRequired: false, sortOrder: 0, layoutRow: 1, layoutColumn: 1, layoutWeight: 1);
        contextField.Update(
            "context", "Context", FieldDefinitionType.LongText,
            FieldDefinitionScope.Header, false, 0, null, 1, 1, 1, 1, 1);

        foreach (var obsoleteField in activeFields.Where(field => field.Id != contextField.Id))
        {
            obsoleteField.Deactivate(now);
        }
    }

    private async Task EnsureTodoAsync(
        IReadOnlyList<TaskTemplate> templates,
        Guid? ownerUserId,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var headerLayout = SerializeLayout([new TaskTemplateLayoutRowResponse(1, [1], HeaderHeight)]);
        var entryLayout = SerializeLayout([new TaskTemplateLayoutRowResponse(1, [4, 1], EntryHeight)]);
        var template = templates.FirstOrDefault(candidate =>
            candidate.BuiltInKind == TaskTemplateBuiltInKind.Todo) ??
            templates.FirstOrDefault(candidate =>
                candidate.BuiltInKind == TaskTemplateBuiltInKind.None &&
                string.Equals(candidate.Name, TodoName, StringComparison.OrdinalIgnoreCase));

        if (template is null)
        {
            template = TaskTemplate.Create(ownerUserId, TodoName, now);
            await _repository.AddAsync(template, cancellationToken);
        }

        var activeFields = template.FieldDefinitions.Where(field => field.IsActive).ToList();
        var itemField = activeFields.FirstOrDefault(field =>
            field.Type == FieldDefinitionType.Text &&
            (string.Equals(field.Key, "item", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(field.Key, "next_step", StringComparison.OrdinalIgnoreCase)));
        var doneField = activeFields.FirstOrDefault(field =>
            field.Type == FieldDefinitionType.Checkbox &&
            string.Equals(field.Key, "done", StringComparison.OrdinalIgnoreCase));
        var descriptionField = activeFields.FirstOrDefault(field =>
            field.Type == FieldDefinitionType.LongText &&
            field.Scope == FieldDefinitionScope.Header);

        if (template.BuiltInKind == TaskTemplateBuiltInKind.Todo &&
            template.Name == TodoName &&
            template.HeaderLayoutJson == headerLayout &&
            template.EntryLayoutJson == entryLayout &&
            activeFields.Count == 3 &&
            descriptionField is not null &&
            FieldMatches(
                descriptionField, "description", "Description", FieldDefinitionType.LongText,
                FieldDefinitionScope.Header, false, 0, 1, 1, 1) &&
            itemField is not null &&
            FieldMatches(
                itemField, "item", "Item", FieldDefinitionType.Text,
                FieldDefinitionScope.Entry, true, 0, 1, 1, 4) &&
            doneField is not null &&
            FieldMatches(
                doneField, "done", "Done", FieldDefinitionType.Checkbox,
                FieldDefinitionScope.Entry, false, 1, 1, 2, 1))
        {
            return;
        }

        template.RestoreBuiltInDefinition(
            TaskTemplateBuiltInKind.Todo,
            TodoName,
            headerLayout,
            entryLayout,
            now);

        descriptionField ??= template.AddFieldDefinition(
            "description",
            "Description",
            FieldDefinitionType.LongText,
            FieldDefinitionScope.Header,
            isRequired: false,
            sortOrder: 0,
            layoutRow: 1,
            layoutColumn: 1,
            layoutWeight: 1);
        descriptionField.Update(
            "description", "Description", FieldDefinitionType.LongText,
            FieldDefinitionScope.Header, false, 0, null, 1, 1, 1, 1, 1);

        itemField ??= template.AddFieldDefinition(
            "item", "Item", FieldDefinitionType.Text, FieldDefinitionScope.Entry,
            isRequired: true, sortOrder: 0, layoutRow: 1, layoutColumn: 1, layoutWeight: 4);
        itemField.Update(
            "item", "Item", FieldDefinitionType.Text,
            FieldDefinitionScope.Entry, true, 0, null, 1, 1, 1, 1, 4);

        doneField ??= template.AddFieldDefinition(
            "done", "Done", FieldDefinitionType.Checkbox, FieldDefinitionScope.Entry,
            isRequired: false, sortOrder: 1, layoutRow: 1, layoutColumn: 2, layoutWeight: 1);
        doneField.Update(
            "done", "Done", FieldDefinitionType.Checkbox,
            FieldDefinitionScope.Entry, false, 1, null, 1, 2, 1, 1, 1);

        foreach (var obsoleteField in activeFields.Where(field =>
                     field.Id != descriptionField.Id &&
                     field.Id != itemField.Id &&
                     field.Id != doneField.Id))
        {
            obsoleteField.Deactivate(now);
        }
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
