using System.Net;
using System.Net.Http.Json;
using DumpTether.App.Projects;
using DumpTether.App.Tasks;
using DumpTether.App.Templates;
using Xunit;

namespace DumpTether.Api.Tests;

public sealed class TaskWallApiTests
{
    [Fact]
    public async Task PostTaskItems_QuickCreateAddsTaskToActiveWall()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var created = await CreateTaskItemAsync(client, "Call Jan");
        var taskItems = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks");

        Assert.NotNull(taskItems);
        Assert.Contains(taskItems, taskItem => taskItem.Id == created.Id);
    }

    [Fact]
    public async Task PostTaskItems_CanCreateTaskWithCategoryProjectAndTemplate()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var project = await CreateProjectAsync(client, "Procurement");
        var template = (await client.GetFromJsonAsync<List<TaskTemplateSummaryResponse>>(
            "/api/templates"))!
            .Single(candidate => candidate.Name == "Basic Task");

        var response = await client.PostAsJsonAsync(
            "/api/tasks",
            new
            {
                title = "Order trackers",
                projectId = project.Id,
                category = project.Name,
                taskTemplateId = template.Id
            });
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>();

        Assert.NotNull(created);
        Assert.Equal(project.Id, created.ProjectId);
        Assert.Equal(project.Name, created.Category);
        Assert.Equal(template.Id, created.TaskTemplateId);
    }

    [Fact]
    public async Task PatchTaskItem_ProjectSelectionUpdatesCategory()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var project = await CreateProjectAsync(client, "Travel");
        var created = await CreateTaskItemAsync(client, "Book hotel");

        var updated = await PatchTaskItemAsync(
            client,
            created.Id,
            new
            {
                projectId = project.Id,
                category = project.Name
            });

        Assert.Equal(project.Id, updated.ProjectId);
        Assert.Equal(project.Name, updated.Category);
        Assert.Contains(updated.TimelineEntries, entry =>
            entry.Kind == "CategoryChanged" &&
            entry.Summary.Contains(project.Name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PostTaskTimeline_QuickNoteAddsNoteAndTouchesTask()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Needs a note");

        await Task.Delay(10);

        var updated = await PostNoteAsync(client, created.Id, "Jan replied with a date.");

        Assert.True(updated.LastTouchedAt > created.LastTouchedAt);
        Assert.Equal(1, updated.NoteCount);
        Assert.Contains(updated.TimelineEntries, entry =>
            entry.Kind == "NoteAdded" &&
            entry.Details == "Jan replied with a date.");
    }

    [Fact]
    public async Task PatchTaskTimeline_EditsNote()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Edit a note");
        var withNote = await PostNoteAsync(client, created.Id, "Original note");
        var note = withNote.TimelineEntries.Single(entry => entry.Kind == "NoteAdded");

        using var message = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/tasks/{created.Id}/timeline/{note.Id}")
        {
            Content = JsonContent.Create(new { note = "Edited note" })
        };
        var response = await client.SendAsync(message);
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>();
        Assert.NotNull(updated);
        Assert.Contains(updated.TimelineEntries, entry =>
            entry.Id == note.Id &&
            entry.Details == "Edited note");
        Assert.True(updated.LastTouchedAt > withNote.LastTouchedAt);
    }

    [Fact]
    public async Task DeleteTaskTimeline_SoftDeletesNote()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Delete a note");
        var withNote = await PostNoteAsync(client, created.Id, "Remove this");
        var note = withNote.TimelineEntries.Single(entry => entry.Kind == "NoteAdded");

        var response = await client.DeleteAsync($"/api/tasks/{created.Id}/timeline/{note.Id}");
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>();
        Assert.NotNull(updated);
        Assert.Equal(0, updated.NoteCount);
        Assert.DoesNotContain(updated.TimelineEntries, entry => entry.Id == note.Id);
    }

    [Fact]
    public async Task PatchTaskItem_StatusCategoryAndColorTouchTask()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Color me");

        await Task.Delay(10);

        var updated = await PatchTaskItemAsync(
            client,
            created.Id,
            new
            {
                status = "Waiting",
                category = "Procurement",
                color = "#F59E0B"
            });

        Assert.Equal("Waiting", updated.Status);
        Assert.Equal("Procurement", updated.Category);
        Assert.Equal("#F59E0B", updated.Color);
        Assert.True(updated.LastTouchedAt > created.LastTouchedAt);
    }

    [Fact]
    public async Task PatchTaskItem_InvalidColorReturnsBadRequest()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Bad color");

        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/tasks/{created.Id}")
        {
            Content = JsonContent.Create(new { color = "red" })
        };

        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTasks_FiltersByStatusCategoryAndColor()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var matching = await CreateTaskItemAsync(client, "Wall filter match");
        var skipped = await CreateTaskItemAsync(client, "Wall filter skip");

        await PatchTaskItemAsync(
            client,
            matching.Id,
            new
            {
                status = "Waiting",
                category = "Procurement",
                color = "#22C55E"
            });
        await PatchTaskItemAsync(
            client,
            skipped.Id,
            new
            {
                status = "Active",
                category = "Personal",
                color = "#60A5FA"
            });

        var statusItems = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks?status=Waiting");
        var categoryItems = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks?category=Procurement");
        var colorItems = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks?color=%2322C55E");

        Assert.NotNull(statusItems);
        Assert.NotNull(categoryItems);
        Assert.NotNull(colorItems);
        Assert.Contains(statusItems, taskItem => taskItem.Id == matching.Id);
        Assert.DoesNotContain(statusItems, taskItem => taskItem.Id == skipped.Id);
        Assert.Contains(categoryItems, taskItem => taskItem.Id == matching.Id);
        Assert.DoesNotContain(categoryItems, taskItem => taskItem.Id == skipped.Id);
        Assert.Contains(colorItems, taskItem => taskItem.Id == matching.Id);
        Assert.DoesNotContain(colorItems, taskItem => taskItem.Id == skipped.Id);
    }

    private static async Task<TaskItemDetailResponse> CreateTaskItemAsync(
        HttpClient client,
        string title)
    {
        var response = await client.PostAsJsonAsync("/api/tasks", new { title });
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>();
        Assert.NotNull(created);

        return created;
    }

    private static async Task<ProjectResponse> CreateProjectAsync(
        HttpClient client,
        string name)
    {
        var response = await client.PostAsJsonAsync("/api/projects", new { name });
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<ProjectResponse>();
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

    private static async Task<TaskItemDetailResponse> PostNoteAsync(
        HttpClient client,
        Guid id,
        string note)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/tasks/{id}/timeline",
            new { note });
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>();
        Assert.NotNull(updated);

        return updated;
    }
}
