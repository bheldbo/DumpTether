using System.Net;
using System.Net.Http.Json;
using DumpTether.App.ArchiveResolutions;
using DumpTether.App.Tasks;
using DumpTether.Data;
using DumpTether.Domain;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DumpTether.Api.Tests;

public sealed class TaskItemsApiTests
{
    [Fact]
    public async Task PostTaskItems_CreatesTaskItem()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var created = await CreateTaskItemAsync(client, "Capture API notes");

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.NotEqual(Guid.Empty, created.WorkspaceId);
        Assert.True(created.ProjectId.HasValue);
        Assert.Equal("Capture API notes", created.Title);
        Assert.Equal(created.CreatedAt, created.LastTouchedAt);
    }

    [Fact]
    public async Task GetTaskItems_ListsTaskItems()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "List me");

        var response = await client.GetAsync("/api/tasks");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected success, got {response.StatusCode}. Body: {body}");

        var taskItems = await response.Content.ReadFromJsonAsync<List<TaskItemSummaryResponse>>();

        Assert.NotNull(taskItems);
        Assert.Contains(taskItems, taskItem => taskItem.Id == created.Id);
    }

    [Fact]
    public async Task GetTaskItemById_ReturnsTaskItem()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Find me");

        var found = await client.GetFromJsonAsync<TaskItemDetailResponse>($"/api/tasks/{created.Id}");

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
        Assert.Equal("Find me", found.Title);
    }

    [Fact]
    public async Task PatchTaskItem_UpdatesTaskItem()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Before update");
        var followUpAt = DateTimeOffset.UtcNow.AddDays(1);

        var updated = await PatchTaskItemAsync(
            client,
            created.Id,
            new
            {
                title = "After update",
                status = "In Progress",
                followUpAt
            });

        Assert.Equal("After update", updated.Title);
        Assert.Equal("In Progress", updated.Status);
        Assert.Equal(followUpAt, updated.FollowUpAt);
    }

    [Fact]
    public async Task PatchTaskItem_MeaningfulChange_UpdatesLastTouchedAt()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Touch me");

        await Task.Delay(10);

        var updated = await PatchTaskItemAsync(
            client,
            created.Id,
            new
            {
                status = "Waiting"
            });

        Assert.True(updated.LastTouchedAt > created.LastTouchedAt);
    }

    [Fact]
    public async Task PostTaskItems_CreatesInitialTimelineEntry()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var created = await CreateTaskItemAsync(client, "Prove creation");

        var entry = Assert.Single(created.TimelineEntries);
        Assert.Equal("Created", entry.Kind);
        Assert.Equal("Task item created", entry.Summary);
        Assert.Equal(created.CreatedAt, entry.OccurredAt);
    }

    [Fact]
    public async Task PostTaskTimeline_AddsTimelineNote()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Take timeline note");

        await Task.Delay(10);

        var updated = await PostTimelineEntryAsync(
            client,
            created.Id,
            new
            {
                note = "Found the source note."
            });

        Assert.True(updated.LastTouchedAt > created.LastTouchedAt);
        Assert.Equal(2, updated.TimelineEntries.Count);

        var entry = updated.TimelineEntries.Last();
        Assert.Equal("NoteAdded", entry.Kind);
        Assert.Equal("Note added", entry.Summary);
        Assert.Equal("Found the source note.", entry.Details);
    }

    [Fact]
    public async Task PostTaskArchive_WithValidResolution_ArchivesTaskItem()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Archive me");
        var archiveResolutionId = await CreateArchiveResolutionAsync(
            factory,
            created.WorkspaceId,
            "Completed");

        var archived = await PostArchiveAsync(
            client,
            created.Id,
            new
            {
                archiveResolutionId,
                note = "Done and captured."
            });

        Assert.NotNull(archived.ArchivedAt);
        Assert.Equal(archiveResolutionId, archived.ArchiveResolutionId);

        var entry = archived.TimelineEntries.Last();
        Assert.Equal("Archived", entry.Kind);
        Assert.Equal("Archived as Completed", entry.Summary);
        Assert.Equal("Done and captured.", entry.Details);
    }

    [Fact]
    public async Task PostTaskArchive_WithoutResolution_ReturnsBadRequest()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Reject archive");

        var response = await client.PostAsJsonAsync(
            $"/api/tasks/{created.Id}/archive",
            new
            {
                note = "No resolution selected."
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTaskArchive_WhenResolutionRequiresExplanationWithoutNote_ReturnsBadRequest()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Explain archive");
        var archiveResolutionId = await CreateArchiveResolutionAsync(
            factory,
            created.WorkspaceId,
            "Blocked",
            requiresExplanation: true);

        var response = await client.PostAsJsonAsync(
            $"/api/tasks/{created.Id}/archive",
            new
            {
                archiveResolutionId
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTaskItems_ExcludesArchivedTaskItemsByDefault()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Hide when archived");
        var archiveResolutionId = await CreateArchiveResolutionAsync(
            factory,
            created.WorkspaceId,
            "Completed");

        await PostArchiveAsync(
            client,
            created.Id,
            new
            {
                archiveResolutionId,
                note = "Done."
            });

        var taskItems = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks");

        Assert.NotNull(taskItems);
        Assert.DoesNotContain(taskItems, taskItem => taskItem.Id == created.Id);
    }

    [Fact]
    public async Task GetTaskItems_WithArchiveScope_ReturnsArchivedTaskItems()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Show in archive");
        var archiveResolutionId = await CreateArchiveResolutionAsync(
            factory,
            created.WorkspaceId,
            "Completed");

        await PostArchiveAsync(
            client,
            created.Id,
            new
            {
                archiveResolutionId,
                note = "Done."
            });

        var taskItems = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks?scope=Archive");

        Assert.NotNull(taskItems);
        Assert.Contains(taskItems, taskItem => taskItem.Id == created.Id);
    }

    [Fact]
    public async Task GetArchiveResolutions_ReturnsDevelopmentArchiveResolutions()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var archiveResolutions = await client.GetFromJsonAsync<List<ArchiveResolutionResponse>>(
            "/api/archive-resolutions");

        Assert.NotNull(archiveResolutions);
        Assert.Contains(archiveResolutions, resolution => resolution.Name == "Completed");
        Assert.Contains(archiveResolutions, resolution =>
            resolution.Name == "Blocked" &&
            resolution.RequiresExplanation);
    }

    [Fact]
    public async Task PostTaskReopen_ReopensArchivedTaskItem()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Reopen me");
        var archiveResolutionId = await CreateArchiveResolutionAsync(
            factory,
            created.WorkspaceId,
            "Completed");

        await PostArchiveAsync(
            client,
            created.Id,
            new
            {
                archiveResolutionId,
                note = "Closed too soon."
            });

        var reopened = await PostReopenAsync(
            client,
            created.Id,
            new
            {
                note = "Needs another pass."
            });

        Assert.Null(reopened.ArchivedAt);
        Assert.Null(reopened.ArchiveResolutionId);

        var entry = reopened.TimelineEntries.Last();
        Assert.Equal("Reopened", entry.Kind);
        Assert.Equal("Task item reopened", entry.Summary);
        Assert.Equal("Needs another pass.", entry.Details);

        var taskItems = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks");
        Assert.NotNull(taskItems);
        Assert.Contains(taskItems, taskItem => taskItem.Id == created.Id);
    }

    private static async Task<TaskItemDetailResponse> CreateTaskItemAsync(
        HttpClient client,
        string title)
    {
        var response = await client.PostAsJsonAsync("/api/tasks", new { title });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created, got {response.StatusCode}. Body: {body}");

        var created = await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>();
        Assert.NotNull(created);

        return created;
    }

    private static async Task<TaskItemDetailResponse> PatchTaskItemAsync(
        HttpClient client,
        Guid id,
        object request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/tasks/{id}")
        {
            Content = JsonContent.Create(request)
        };

        var response = await client.SendAsync(message);
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>();
        Assert.NotNull(updated);

        return updated;
    }

    private static async Task<TaskItemDetailResponse> PostTimelineEntryAsync(
        HttpClient client,
        Guid id,
        object request)
    {
        var response = await client.PostAsJsonAsync($"/api/tasks/{id}/timeline", request);
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>();
        Assert.NotNull(updated);

        return updated;
    }

    private static async Task<TaskItemDetailResponse> PostArchiveAsync(
        HttpClient client,
        Guid id,
        object request)
    {
        var response = await client.PostAsJsonAsync($"/api/tasks/{id}/archive", request);
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>();
        Assert.NotNull(updated);

        return updated;
    }

    private static async Task<TaskItemDetailResponse> PostReopenAsync(
        HttpClient client,
        Guid id,
        object request)
    {
        var response = await client.PostAsJsonAsync($"/api/tasks/{id}/reopen", request);
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>();
        Assert.NotNull(updated);

        return updated;
    }

    private static async Task<Guid> CreateArchiveResolutionAsync(
        DumpTetherApiFactory factory,
        Guid workspaceId,
        string name,
        bool requiresExplanation = false)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        var existingResolution = dbContext.ArchiveResolutions.SingleOrDefault(
            archiveResolution =>
                archiveResolution.WorkspaceId == workspaceId &&
                archiveResolution.Name == name);

        if (existingResolution is not null)
        {
            return existingResolution.Id;
        }

        var archiveResolution = ArchiveResolution.Create(
            workspaceId,
            name,
            DateTimeOffset.UtcNow,
            requiresExplanation
                ? "Test resolution requires an archive note."
                : null,
            requiresExplanation);

        dbContext.ArchiveResolutions.Add(archiveResolution);
        await dbContext.SaveChangesAsync();

        return archiveResolution.Id;
    }
}
