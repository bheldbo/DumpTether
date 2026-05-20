namespace DumpTether.Domain;

public sealed class FieldValue
{
    private FieldValue()
    {
    }

    private FieldValue(
        Guid id,
        Guid taskItemId,
        Guid fieldDefinitionId,
        string valueJson,
        DateTimeOffset updatedAt)
    {
        Id = id;
        TaskItemId = taskItemId;
        FieldDefinitionId = fieldDefinitionId;
        ValueJson = valueJson;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; private set; }

    public Guid TaskItemId { get; private set; }

    public Guid FieldDefinitionId { get; private set; }

    public string ValueJson { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; private set; }

    public static FieldValue Create(
        Guid taskItemId,
        Guid fieldDefinitionId,
        string valueJson,
        DateTimeOffset updatedAt)
    {
        DomainGuards.NotEmpty(taskItemId, nameof(taskItemId));
        DomainGuards.NotEmpty(fieldDefinitionId, nameof(fieldDefinitionId));

        return new FieldValue(
            Guid.NewGuid(),
            taskItemId,
            fieldDefinitionId,
            DomainGuards.NotBlank(valueJson, nameof(valueJson)),
            updatedAt);
    }

    public void UpdateValue(string valueJson, DateTimeOffset updatedAt)
    {
        ValueJson = DomainGuards.NotBlank(valueJson, nameof(valueJson));
        UpdatedAt = updatedAt;
    }
}
