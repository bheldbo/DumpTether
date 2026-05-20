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
        int sortOrder)
    {
        Id = id;
        TaskTemplateId = taskTemplateId;
        Key = key;
        Label = label;
        Type = type;
        IsRequired = isRequired;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }

    public Guid TaskTemplateId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public FieldDefinitionType Type { get; private set; }

    public bool IsRequired { get; private set; }

    public int SortOrder { get; private set; }

    public static FieldDefinition Create(
        Guid taskTemplateId,
        string key,
        string label,
        FieldDefinitionType type,
        bool isRequired,
        int sortOrder)
    {
        DomainGuards.NotEmpty(taskTemplateId, nameof(taskTemplateId));

        return new FieldDefinition(
            Guid.NewGuid(),
            taskTemplateId,
            DomainGuards.NotBlank(key, nameof(key)),
            DomainGuards.NotBlank(label, nameof(label)),
            type,
            isRequired,
            sortOrder);
    }
}
