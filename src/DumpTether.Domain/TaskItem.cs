namespace DumpTether.Domain;

public sealed class TaskItem
{
    private readonly List<FieldValue> _fieldValues = [];
    private readonly List<TaskTimelineEntry> _timelineEntries = [];

    private TaskItem()
    {
    }

    private TaskItem(
        Guid id,
        Guid workspaceId,
        Guid? projectId,
        Guid? taskTemplateId,
        string title,
        DateTimeOffset createdAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        ProjectId = projectId;
        TaskTemplateId = taskTemplateId;
        Title = title;
        CreatedAt = createdAt;
        LastTouchedAt = createdAt;

        AddTimelineEntry(
            TaskTimelineEntryKind.Created,
            "Task item created",
            createdAt,
            null);
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public Guid? ProjectId { get; private set; }

    public Guid? TaskTemplateId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastViewedAt { get; private set; }

    public DateTimeOffset LastTouchedAt { get; private set; }

    public DateTimeOffset? FollowUpAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public Guid? ArchiveResolutionId { get; private set; }

    public IReadOnlyCollection<FieldValue> FieldValues => _fieldValues.AsReadOnly();

    public IReadOnlyCollection<TaskTimelineEntry> TimelineEntries => _timelineEntries.AsReadOnly();

    public static TaskItem Create(
        Guid workspaceId,
        Guid? projectId,
        string title,
        DateTimeOffset createdAt,
        Guid? taskTemplateId = null)
    {
        DomainGuards.NotEmpty(workspaceId, nameof(workspaceId));

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(projectId));
        }

        if (taskTemplateId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(taskTemplateId));
        }

        return new TaskItem(
            Guid.NewGuid(),
            workspaceId,
            projectId,
            taskTemplateId,
            DomainGuards.NotBlank(title, nameof(title)),
            createdAt);
    }

    public void MarkViewed(DateTimeOffset viewedAt)
    {
        LastViewedAt = viewedAt;
    }

    public void Rename(string title, DateTimeOffset occurredAt)
    {
        var normalizedTitle = DomainGuards.NotBlank(title, nameof(title));

        if (Title == normalizedTitle)
        {
            return;
        }

        var previousTitle = Title;
        Title = normalizedTitle;

        AddTimelineEntry(
            TaskTimelineEntryKind.TitleChanged,
            "Title changed",
            occurredAt,
            $"From \"{previousTitle}\" to \"{Title}\"");
    }

    public void SetFollowUp(DateTimeOffset? followUpAt, DateTimeOffset occurredAt)
    {
        if (FollowUpAt == followUpAt)
        {
            return;
        }

        FollowUpAt = followUpAt;

        AddTimelineEntry(
            TaskTimelineEntryKind.FollowUpChanged,
            "Follow-up changed",
            occurredAt,
            followUpAt.HasValue
                ? $"Follow-up set to {followUpAt.Value:O}"
                : "Follow-up cleared");
    }

    public void AddNote(string note, DateTimeOffset occurredAt)
    {
        AddTimelineEntry(
            TaskTimelineEntryKind.NoteAdded,
            "Note added",
            occurredAt,
            DomainGuards.NotBlank(note, nameof(note)));
    }

    public void ChangeStatus(string status, DateTimeOffset occurredAt, string? note = null)
    {
        Status = DomainGuards.NotBlank(status, nameof(status));

        AddTimelineEntry(
            TaskTimelineEntryKind.StatusChanged,
            $"Status changed to {Status}",
            occurredAt,
            DomainGuards.OptionalTrimmed(note));
    }

    public FieldValue SetFieldValue(
        FieldDefinition fieldDefinition,
        string valueJson,
        DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(fieldDefinition);

        var existingValue = _fieldValues.FirstOrDefault(value =>
            value.FieldDefinitionId == fieldDefinition.Id);

        if (existingValue is not null)
        {
            existingValue.UpdateValue(valueJson, updatedAt);
            AddTimelineEntry(
                TaskTimelineEntryKind.FieldValueChanged,
                $"Field value changed: {fieldDefinition.Label}",
                updatedAt,
                fieldDefinition.Key);

            return existingValue;
        }

        var fieldValue = FieldValue.Create(Id, fieldDefinition.Id, valueJson, updatedAt);
        _fieldValues.Add(fieldValue);

        AddTimelineEntry(
            TaskTimelineEntryKind.FieldValueChanged,
            $"Field value changed: {fieldDefinition.Label}",
            updatedAt,
            fieldDefinition.Key);

        return fieldValue;
    }

    public void Archive(
        ArchiveResolution archiveResolution,
        DateTimeOffset archivedAt,
        string? note = null)
    {
        ArgumentNullException.ThrowIfNull(archiveResolution);

        if (archiveResolution.WorkspaceId != WorkspaceId)
        {
            throw new InvalidOperationException(
                "Archive resolution must belong to the same workspace as the task item.");
        }

        var normalizedNote = DomainGuards.OptionalTrimmed(note);

        if (archiveResolution.RequiresExplanation && string.IsNullOrWhiteSpace(normalizedNote))
        {
            throw new InvalidOperationException(
                "Archive note is required for the selected archive resolution.");
        }

        ArchivedAt = archivedAt;
        ArchiveResolutionId = archiveResolution.Id;

        AddTimelineEntry(
            TaskTimelineEntryKind.Archived,
            $"Archived as {archiveResolution.Name}",
            archivedAt,
            normalizedNote);
    }

    public void Reopen(DateTimeOffset reopenedAt, string? note = null)
    {
        if (ArchivedAt is null)
        {
            throw new InvalidOperationException("Only archived task items can be reopened.");
        }

        ArchivedAt = null;
        ArchiveResolutionId = null;

        AddTimelineEntry(
            TaskTimelineEntryKind.Reopened,
            "Task item reopened",
            reopenedAt,
            DomainGuards.OptionalTrimmed(note));
    }

    private void AddTimelineEntry(
        TaskTimelineEntryKind kind,
        string summary,
        DateTimeOffset occurredAt,
        string? details)
    {
        _timelineEntries.Add(TaskTimelineEntry.Create(Id, kind, summary, occurredAt, details));
        LastTouchedAt = occurredAt;
    }
}
