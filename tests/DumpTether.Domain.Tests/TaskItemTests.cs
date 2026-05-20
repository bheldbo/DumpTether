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
}
