namespace DumpTether.Domain;

public sealed class TaskTimelineEntry
{
    private TaskTimelineEntry()
    {
    }

    private TaskTimelineEntry(
        Guid id,
        Guid taskItemId,
        TaskTimelineEntryKind kind,
        string summary,
        string? details,
        DateTimeOffset occurredAt)
    {
        Id = id;
        TaskItemId = taskItemId;
        Kind = kind;
        Summary = summary;
        Details = details;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public Guid TaskItemId { get; private set; }

    public TaskTimelineEntryKind Kind { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public string? Details { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static TaskTimelineEntry Create(
        Guid taskItemId,
        TaskTimelineEntryKind kind,
        string summary,
        DateTimeOffset occurredAt,
        string? details = null)
    {
        DomainGuards.NotEmpty(taskItemId, nameof(taskItemId));

        return new TaskTimelineEntry(
            Guid.NewGuid(),
            taskItemId,
            kind,
            DomainGuards.NotBlank(summary, nameof(summary)),
            DomainGuards.OptionalTrimmed(details),
            occurredAt);
    }
}
