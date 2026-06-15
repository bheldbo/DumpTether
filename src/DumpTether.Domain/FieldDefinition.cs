namespace DumpTether.Domain;

public sealed class FieldDefinition
{
    private FieldDefinition()
    {
    }

    private FieldDefinition(
        Guid id,
        Guid taskTemplateId,
        string key,
        string label,
        FieldDefinitionType type,
        FieldDefinitionScope scope,
        bool isRequired,
        int sortOrder,
        string? optionsJson,
        int layoutRow,
        int layoutColumn,
        int layoutRowSpan,
        int layoutColumnSpan)
    {
        Id = id;
        TaskTemplateId = taskTemplateId;
        Key = key;
        Label = label;
        Type = type;
        Scope = scope;
        IsRequired = isRequired;
        SortOrder = sortOrder;
        OptionsJson = optionsJson;
        LayoutRow = layoutRow;
        LayoutColumn = layoutColumn;
        LayoutRowSpan = layoutRowSpan;
        LayoutColumnSpan = layoutColumnSpan;
    }

    public Guid Id { get; private set; }

    public Guid TaskTemplateId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public FieldDefinitionType Type { get; private set; }

    public FieldDefinitionScope Scope { get; private set; } = FieldDefinitionScope.Header;

    public bool IsRequired { get; private set; }

    public int SortOrder { get; private set; }

    public string? OptionsJson { get; private set; }

    public int LayoutRow { get; private set; } = 1;

    public int LayoutColumn { get; private set; } = 1;

    public int LayoutRowSpan { get; private set; } = 1;

    public int LayoutColumnSpan { get; private set; } = 1;

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public bool IsActive => DeactivatedAt is null;

    public static FieldDefinition Create(
        Guid taskTemplateId,
        string key,
        string label,
        FieldDefinitionType type,
        FieldDefinitionScope scope,
        bool isRequired,
        int sortOrder,
        string? optionsJson = null,
        int layoutRow = 1,
        int layoutColumn = 1,
        int layoutRowSpan = 1,
        int layoutColumnSpan = 1)
    {
        DomainGuards.NotEmpty(taskTemplateId, nameof(taskTemplateId));
        ValidateOptions(type, optionsJson);
        ValidateLayout(layoutRow, layoutColumn, layoutRowSpan, layoutColumnSpan);

        return new FieldDefinition(
            Guid.NewGuid(),
            taskTemplateId,
            DomainGuards.NotBlank(key, nameof(key)),
            DomainGuards.NotBlank(label, nameof(label)),
            type,
            scope,
            isRequired,
            sortOrder,
            optionsJson,
            layoutRow,
            layoutColumn,
            layoutRowSpan,
            layoutColumnSpan);
    }

    public void Update(
        string key,
        string label,
        FieldDefinitionType type,
        FieldDefinitionScope scope,
        bool isRequired,
        int sortOrder,
        string? optionsJson = null,
        int layoutRow = 1,
        int layoutColumn = 1,
        int layoutRowSpan = 1,
        int layoutColumnSpan = 1)
    {
        ValidateOptions(type, optionsJson);
        ValidateLayout(layoutRow, layoutColumn, layoutRowSpan, layoutColumnSpan);

        Key = DomainGuards.NotBlank(key, nameof(key));
        Label = DomainGuards.NotBlank(label, nameof(label));
        Type = type;
        Scope = scope;
        IsRequired = isRequired;
        SortOrder = sortOrder;
        OptionsJson = optionsJson;
        LayoutRow = layoutRow;
        LayoutColumn = layoutColumn;
        LayoutRowSpan = layoutRowSpan;
        LayoutColumnSpan = layoutColumnSpan;
        DeactivatedAt = null;
    }

    public void Deactivate(DateTimeOffset deactivatedAt)
    {
        DeactivatedAt ??= deactivatedAt;
    }

    private static void ValidateOptions(FieldDefinitionType type, string? optionsJson)
    {
        if (type == FieldDefinitionType.Select && string.IsNullOrWhiteSpace(optionsJson))
        {
            throw new ArgumentException(
                "Select fields require at least one option.",
                nameof(optionsJson));
        }
    }

    private static void ValidateLayout(
        int layoutRow,
        int layoutColumn,
        int layoutRowSpan,
        int layoutColumnSpan)
    {
        if (layoutRow is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(layoutRow), "Layout row must be between 1 and 12.");
        }

        if (layoutColumn is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(layoutColumn), "Layout column must be between 1 and 12.");
        }

        if (layoutRowSpan is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(layoutRowSpan), "Layout row span must be between 1 and 6.");
        }

        if (layoutColumnSpan is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(layoutColumnSpan), "Layout column span must be between 1 and 12.");
        }
    }
}
