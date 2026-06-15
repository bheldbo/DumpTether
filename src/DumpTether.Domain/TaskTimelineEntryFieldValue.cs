namespace DumpTether.Domain;

public sealed class TaskTimelineEntryFieldValue
{
    private TaskTimelineEntryFieldValue()
    {
    }

    private TaskTimelineEntryFieldValue(
        Guid id,
        Guid taskTimelineEntryId,
        Guid fieldDefinitionId,
        string valueJson,
        DateTimeOffset updatedAt)
    {
        Id = id;
        TaskTimelineEntryId = taskTimelineEntryId;
        FieldDefinitionId = fieldDefinitionId;
        ValueJson = valueJson;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; private set; }

    public Guid TaskTimelineEntryId { get; private set; }

    public Guid FieldDefinitionId { get; private set; }

    public string ValueJson { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; private set; }

    public static TaskTimelineEntryFieldValue Create(
        Guid taskTimelineEntryId,
        Guid fieldDefinitionId,
        string valueJson,
        DateTimeOffset updatedAt)
    {
        DomainGuards.NotEmpty(taskTimelineEntryId, nameof(taskTimelineEntryId));
        DomainGuards.NotEmpty(fieldDefinitionId, nameof(fieldDefinitionId));

        return new TaskTimelineEntryFieldValue(
            Guid.NewGuid(),
            taskTimelineEntryId,
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
