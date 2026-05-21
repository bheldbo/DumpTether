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
        bool isRequired,
        int sortOrder,
        string? optionsJson)
    {
        Id = id;
        TaskTemplateId = taskTemplateId;
        Key = key;
        Label = label;
        Type = type;
        IsRequired = isRequired;
        SortOrder = sortOrder;
        OptionsJson = optionsJson;
    }

    public Guid Id { get; private set; }

    public Guid TaskTemplateId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public FieldDefinitionType Type { get; private set; }

    public bool IsRequired { get; private set; }

    public int SortOrder { get; private set; }

    public string? OptionsJson { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public bool IsActive => DeactivatedAt is null;

    public static FieldDefinition Create(
        Guid taskTemplateId,
        string key,
        string label,
        FieldDefinitionType type,
        bool isRequired,
        int sortOrder,
        string? optionsJson = null)
    {
        DomainGuards.NotEmpty(taskTemplateId, nameof(taskTemplateId));
        ValidateOptions(type, optionsJson);

        return new FieldDefinition(
            Guid.NewGuid(),
            taskTemplateId,
            DomainGuards.NotBlank(key, nameof(key)),
            DomainGuards.NotBlank(label, nameof(label)),
            type,
            isRequired,
            sortOrder,
            optionsJson);
    }

    public void Update(
        string key,
        string label,
        FieldDefinitionType type,
        bool isRequired,
        int sortOrder,
        string? optionsJson = null)
    {
        ValidateOptions(type, optionsJson);

        Key = DomainGuards.NotBlank(key, nameof(key));
        Label = DomainGuards.NotBlank(label, nameof(label));
        Type = type;
        IsRequired = isRequired;
        SortOrder = sortOrder;
        OptionsJson = optionsJson;
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
}
