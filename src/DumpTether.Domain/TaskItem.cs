namespace DumpTether.Domain;

public sealed class TaskItem
{
    private readonly List<FieldValue> _fieldValues = [];
    private readonly List<TaskItemShare> _shares = [];
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

    public Guid? ParentTaskItemId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Status { get; private set; }

    public string? Category { get; private set; }

    public string? Color { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastViewedAt { get; private set; }

    public DateTimeOffset LastTouchedAt { get; private set; }

    public DateTimeOffset? FollowUpAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public IReadOnlyCollection<FieldValue> FieldValues => _fieldValues.AsReadOnly();

    public IReadOnlyCollection<TaskItemShare> Shares => _shares.AsReadOnly();

    public IReadOnlyCollection<TaskTimelineEntry> TimelineEntries => _timelineEntries.AsReadOnly();

    public static TaskItem Create(
        Guid workspaceId,
        Guid? projectId,
        string title,
        DateTimeOffset createdAt,
        Guid? taskTemplateId = null)
    {
        return Create(
            Guid.NewGuid(),
            workspaceId,
            projectId,
            title,
            createdAt,
            taskTemplateId);
    }

    public static TaskItem Create(
        Guid id,
        Guid workspaceId,
        Guid? projectId,
        string title,
        DateTimeOffset createdAt,
        Guid? taskTemplateId = null)
    {
        DomainGuards.NotEmpty(id, nameof(id));
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
            id,
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

    public void AssignProject(Guid? projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(projectId));
        }

        ProjectId = projectId;
    }

    public void MakeSubtaskOf(TaskItem parent, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(parent);

        if (parent.Id == Id)
        {
            throw new InvalidOperationException("A task cannot be its own parent.");
        }

        if (parent.WorkspaceId != WorkspaceId)
        {
            throw new InvalidOperationException("A subtask must belong to the same board as its parent.");
        }

        if (parent.ParentTaskItemId.HasValue)
        {
            throw new InvalidOperationException("Nested subtasks are not supported.");
        }

        if (ParentTaskItemId == parent.Id)
        {
            return;
        }

        if (ParentTaskItemId.HasValue)
        {
            throw new InvalidOperationException("A subtask cannot be moved to another parent.");
        }

        ParentTaskItemId = parent.Id;
        AddTimelineEntry(
            TaskTimelineEntryKind.ParentChanged,
            "Added as a subtask",
            occurredAt,
            $"Parent task: {parent.Title}");
    }

    public void RecordSubtaskAdded(TaskItem subtask, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(subtask);

        if (subtask.ParentTaskItemId != Id || subtask.WorkspaceId != WorkspaceId)
        {
            throw new InvalidOperationException("The task is not a child of this parent.");
        }

        AddTimelineEntry(
            TaskTimelineEntryKind.ParentChanged,
            "Subtask added",
            occurredAt,
            subtask.Title);
    }

    public TaskTimelineEntry AddNote(string? note, DateTimeOffset occurredAt)
    {
        return AddTimelineEntry(
            TaskTimelineEntryKind.NoteAdded,
            "Note added",
            occurredAt,
            DomainGuards.OptionalTrimmed(note));
    }

    public TaskTimelineEntry AddNote(
        Guid id,
        string? note,
        DateTimeOffset occurredAt)
    {
        DomainGuards.NotEmpty(id, nameof(id));

        var existingEntry = _timelineEntries.FirstOrDefault(entry => entry.Id == id);
        if (existingEntry is not null)
        {
            return existingEntry;
        }

        var entry = TaskTimelineEntry.Create(
            id,
            Id,
            TaskTimelineEntryKind.NoteAdded,
            "Note added",
            occurredAt,
            DomainGuards.OptionalTrimmed(note));
        _timelineEntries.Add(entry);
        LastTouchedAt = occurredAt;

        return entry;
    }

    public void EditNote(Guid noteId, string note, DateTimeOffset occurredAt)
    {
        var entry = GetTimelineEntry(noteId);
        entry.EditNote(note, occurredAt);
        LastTouchedAt = occurredAt;
    }

    public void DeleteNote(Guid noteId, DateTimeOffset occurredAt)
    {
        var entry = GetTimelineEntry(noteId);
        entry.SoftDeleteNote(occurredAt);
        LastTouchedAt = occurredAt;
    }

    public void ChangeStatus(string? status, DateTimeOffset occurredAt, string? note = null)
    {
        var normalizedStatus = DomainGuards.OptionalTrimmed(status);

        if (Status == normalizedStatus)
        {
            return;
        }

        Status = normalizedStatus;

        AddTimelineEntry(
            TaskTimelineEntryKind.StatusChanged,
            Status is null ? "Status cleared" : $"Status changed to {Status}",
            occurredAt,
            DomainGuards.OptionalTrimmed(note));
    }

    public void ChangeCategory(string? category, DateTimeOffset occurredAt)
    {
        var normalizedCategory = DomainGuards.OptionalTrimmed(category);

        if (Category == normalizedCategory)
        {
            return;
        }

        Category = normalizedCategory;

        AddTimelineEntry(
            TaskTimelineEntryKind.CategoryChanged,
            Category is null ? "Category cleared" : $"Category changed to {Category}",
            occurredAt,
            null);
    }

    public void ChangeColor(string? color, DateTimeOffset occurredAt)
    {
        var normalizedColor = DomainGuards.OptionalHexColor(color, nameof(color));

        if (Color == normalizedColor)
        {
            return;
        }

        Color = normalizedColor;

        AddTimelineEntry(
            TaskTimelineEntryKind.ColorChanged,
            Color is null ? "Color cleared" : "Color changed",
            occurredAt,
            Color);
    }

    public TaskItemShare AddShare(
        string email,
        Guid? sharedWithUserId,
        Guid sharedByUserId,
        TaskItemShareRole role,
        string? tokenHash,
        DateTimeOffset? expiresAt,
        DateTimeOffset occurredAt)
    {
        var normalizedEmail = AppUser.NormalizeEmail(email);
        var existingShare = _shares.FirstOrDefault(share =>
            share.RevokedAt is null &&
            string.Equals(share.NormalizedEmail, normalizedEmail, StringComparison.Ordinal));

        if (existingShare is not null)
        {
            if (sharedWithUserId.HasValue)
            {
                existingShare.LinkUser(sharedWithUserId.Value);
            }

            return existingShare;
        }

        var share = TaskItemShare.Create(
            WorkspaceId,
            Id,
            email,
            sharedWithUserId,
            sharedByUserId,
            role,
            tokenHash,
            expiresAt,
            occurredAt);
        _shares.Add(share);

        AddTimelineEntry(
            TaskTimelineEntryKind.Shared,
            "Task shared",
            occurredAt,
            share.Email);

        return share;
    }

    public void RevokeShare(Guid shareId, DateTimeOffset occurredAt)
    {
        DomainGuards.NotEmpty(shareId, nameof(shareId));
        var share = _shares.FirstOrDefault(candidate => candidate.Id == shareId) ??
            throw new InvalidOperationException("Task share was not found.");

        if (share.RevokedAt is not null)
        {
            return;
        }

        share.Revoke(occurredAt);

        AddTimelineEntry(
            TaskTimelineEntryKind.ShareRevoked,
            "Task share removed",
            occurredAt,
            share.Email);
    }

    public void ChangeShareRole(Guid shareId, TaskItemShareRole role, DateTimeOffset occurredAt)
    {
        DomainGuards.NotEmpty(shareId, nameof(shareId));
        var share = _shares.FirstOrDefault(candidate => candidate.Id == shareId) ??
            throw new InvalidOperationException("Task share was not found.");

        if (share.RevokedAt is not null)
        {
            throw new InvalidOperationException("Task share has already been removed.");
        }

        if (!share.ChangeRole(role))
        {
            return;
        }

        AddTimelineEntry(
            TaskTimelineEntryKind.Shared,
            "Task share role changed",
            occurredAt,
            share.Email);
    }

    public bool SetFieldValue(
        FieldDefinition fieldDefinition,
        string valueJson,
        DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(fieldDefinition);

        if (fieldDefinition.Scope != FieldDefinitionScope.Header)
        {
            throw new InvalidOperationException("Only header fields can be set on task items.");
        }

        var normalizedValueJson = DomainGuards.NotBlank(valueJson, nameof(valueJson));

        var existingValue = _fieldValues.FirstOrDefault(value =>
            value.FieldDefinitionId == fieldDefinition.Id);

        if (existingValue is not null)
        {
            if (existingValue.ValueJson == normalizedValueJson)
            {
                return false;
            }

            existingValue.UpdateValue(normalizedValueJson, updatedAt);
            AddTimelineEntry(
                TaskTimelineEntryKind.FieldValueChanged,
                $"Field value changed: {fieldDefinition.Label}",
                updatedAt,
                fieldDefinition.Key);

            return true;
        }

        var fieldValue = FieldValue.Create(Id, fieldDefinition.Id, normalizedValueJson, updatedAt);
        _fieldValues.Add(fieldValue);

        AddTimelineEntry(
            TaskTimelineEntryKind.FieldValueChanged,
            $"Field value changed: {fieldDefinition.Label}",
            updatedAt,
            fieldDefinition.Key);

        return true;
    }

    public bool SetTimelineEntryFieldValue(
        Guid entryId,
        FieldDefinition fieldDefinition,
        string valueJson,
        DateTimeOffset updatedAt)
    {
        var entry = GetTimelineEntry(entryId);
        var changed = entry.SetFieldValue(fieldDefinition, valueJson, updatedAt);

        if (changed)
        {
            LastTouchedAt = updatedAt;
        }

        return changed;
    }

    public void Archive(DateTimeOffset archivedAt)
    {
        if (ArchivedAt.HasValue)
        {
            throw new InvalidOperationException("Task item is already archived.");
        }

        ArchivedAt = archivedAt;

        AddTimelineEntry(
            TaskTimelineEntryKind.Archived,
            "Task item archived",
            archivedAt,
            details: null);
    }

    public void Reopen(DateTimeOffset reopenedAt, string? note = null)
    {
        if (ArchivedAt is null)
        {
            throw new InvalidOperationException("Only archived task items can be reopened.");
        }

        ArchivedAt = null;

        AddTimelineEntry(
            TaskTimelineEntryKind.Reopened,
            "Task item reopened",
            reopenedAt,
            DomainGuards.OptionalTrimmed(note));
    }

    private TaskTimelineEntry AddTimelineEntry(
        TaskTimelineEntryKind kind,
        string summary,
        DateTimeOffset occurredAt,
        string? details)
    {
        var entry = TaskTimelineEntry.Create(Id, kind, summary, occurredAt, details);
        _timelineEntries.Add(entry);
        LastTouchedAt = occurredAt;

        return entry;
    }

    private TaskTimelineEntry GetTimelineEntry(Guid entryId)
    {
        DomainGuards.NotEmpty(entryId, nameof(entryId));

        return _timelineEntries.FirstOrDefault(entry => entry.Id == entryId) ??
            throw new InvalidOperationException("Timeline entry was not found.");
    }

}
