using DumpTether.Domain;
using Xunit;

namespace DumpTether.Domain.Tests;

public sealed class TaskItemTests
{
    [Fact]
    public void Create_SetsRequiredFieldsAndCreatedTimelineEntry()
    {
        var now = new DateTimeOffset(2026, 5, 20, 9, 30, 0, TimeSpan.Zero);
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var taskItem = TaskItem.Create(workspaceId, projectId, "Sort working notes", now);

        Assert.NotEqual(Guid.Empty, taskItem.Id);
        Assert.Equal(workspaceId, taskItem.WorkspaceId);
        Assert.Equal(projectId, taskItem.ProjectId);
        Assert.Equal("Sort working notes", taskItem.Title);
        Assert.Equal(now, taskItem.CreatedAt);
        Assert.Equal(now, taskItem.LastTouchedAt);
        Assert.Null(taskItem.LastViewedAt);
        Assert.Null(taskItem.FollowUpAt);
        Assert.Null(taskItem.ArchivedAt);
        Assert.Null(taskItem.ArchiveResolutionId);

        var entry = Assert.Single(taskItem.TimelineEntries);
        Assert.Equal(TaskTimelineEntryKind.Created, entry.Kind);
        Assert.Equal(taskItem.Id, entry.TaskItemId);
        Assert.Equal(now, entry.OccurredAt);
    }

    [Fact]
    public void AddNote_AddsTimelineEntryAndUpdatesLastTouchedAt()
    {
        var createdAt = new DateTimeOffset(2026, 5, 20, 9, 30, 0, TimeSpan.Zero);
        var noteAt = createdAt.AddMinutes(15);
        var taskItem = TaskItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Review inbox", createdAt);

        taskItem.AddNote("Source note captured from planning session.", noteAt);

        Assert.Equal(noteAt, taskItem.LastTouchedAt);
        Assert.Equal(2, taskItem.TimelineEntries.Count);

        var entry = taskItem.TimelineEntries.Last();
        Assert.Equal(TaskTimelineEntryKind.NoteAdded, entry.Kind);
        Assert.Equal("Note added", entry.Summary);
        Assert.Equal("Source note captured from planning session.", entry.Details);
        Assert.Equal(noteAt, entry.OccurredAt);
    }

    [Fact]
    public void Archive_WithResolution_SetsArchiveStateAndCreatesTimelineEntry()
    {
        var workspaceId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 5, 20, 9, 30, 0, TimeSpan.Zero);
        var archivedAt = createdAt.AddHours(2);
        var archiveResolution = ArchiveResolution.Create(workspaceId, "Completed", createdAt);
        var taskItem = TaskItem.Create(workspaceId, Guid.NewGuid(), "Draft readme", createdAt);

        taskItem.Archive(archiveResolution, archivedAt, "Finished the initial pass.");

        Assert.Equal(archivedAt, taskItem.ArchivedAt);
        Assert.Equal(archiveResolution.Id, taskItem.ArchiveResolutionId);
        Assert.Equal(archivedAt, taskItem.LastTouchedAt);

        var entry = taskItem.TimelineEntries.Last();
        Assert.Equal(TaskTimelineEntryKind.Archived, entry.Kind);
        Assert.Equal($"Archived as {archiveResolution.Name}", entry.Summary);
        Assert.Equal("Finished the initial pass.", entry.Details);
        Assert.Equal(archivedAt, entry.OccurredAt);
    }

    [Fact]
    public void Archive_WithoutResolution_ThrowsAndLeavesTaskItemOpen()
    {
        var createdAt = new DateTimeOffset(2026, 5, 20, 9, 30, 0, TimeSpan.Zero);
        var archivedAt = createdAt.AddHours(1);
        var taskItem = TaskItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Clean up notes", createdAt);

        Assert.Throws<ArgumentNullException>(() => taskItem.Archive(null!, archivedAt));

        Assert.Null(taskItem.ArchivedAt);
        Assert.Null(taskItem.ArchiveResolutionId);
        Assert.Equal(createdAt, taskItem.LastTouchedAt);
        Assert.Single(taskItem.TimelineEntries);
    }

    [Fact]
    public void MakeSubtaskOf_SameWorkspace_RecordsHierarchyAndEvidence()
    {
        var workspaceId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 8, 13, 10, 0, 0, TimeSpan.Zero);
        var parent = TaskItem.Create(workspaceId, null, "Plan the trip", createdAt);
        var child = TaskItem.Create(workspaceId, null, "Book the train", createdAt);

        child.MakeSubtaskOf(parent, createdAt.AddMinutes(1));
        parent.RecordSubtaskAdded(child, createdAt.AddMinutes(1));

        Assert.Equal(parent.Id, child.ParentTaskItemId);
        Assert.Equal(TaskTimelineEntryKind.ParentChanged, child.TimelineEntries.Last().Kind);
        Assert.Equal(TaskTimelineEntryKind.ParentChanged, parent.TimelineEntries.Last().Kind);
        Assert.Equal(createdAt.AddMinutes(1), parent.LastTouchedAt);
    }

    [Fact]
    public void MakeSubtaskOf_DifferentWorkspace_RejectsRelationship()
    {
        var now = DateTimeOffset.UtcNow;
        var parent = TaskItem.Create(Guid.NewGuid(), null, "Parent", now);
        var child = TaskItem.Create(Guid.NewGuid(), null, "Child", now);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            child.MakeSubtaskOf(parent, now));

        Assert.Equal("A subtask must belong to the same board as its parent.", exception.Message);
        Assert.Null(child.ParentTaskItemId);
    }

    [Fact]
    public void MakeSubtaskOf_ChildParent_RejectsNestedSubtask()
    {
        var workspaceId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var root = TaskItem.Create(workspaceId, null, "Root", now);
        var child = TaskItem.Create(workspaceId, null, "Child", now);
        var grandchild = TaskItem.Create(workspaceId, null, "Grandchild", now);
        child.MakeSubtaskOf(root, now);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            grandchild.MakeSubtaskOf(child, now));

        Assert.Equal("Nested subtasks are not supported.", exception.Message);
        Assert.Null(grandchild.ParentTaskItemId);
    }
}
