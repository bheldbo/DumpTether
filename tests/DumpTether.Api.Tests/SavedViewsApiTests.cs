using System.Net;
using System.Net.Http.Json;
using DumpTether.App.Projects;
using DumpTether.App.Tasks;
using DumpTether.App.Views;
using DumpTether.Data;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DumpTether.Api.Tests;

public sealed class SavedViewsApiTests
{
    [Fact]
    public async Task PostViews_CreatesSavedView()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var created = await CreateSavedViewAsync(
            client,
            new
            {
                name = "Waiting work",
                filter = new { status = "Waiting" },
                sort = new { field = "lastTouchedAt", direction = "desc" },
                sortOrder = 20
            });

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Waiting work", created.Name);
        Assert.Equal("Workspace", created.Scope);
        Assert.Equal("Waiting", created.Filter.Status);
        Assert.Equal("lastTouchedAt", created.Sort.Field);
    }

    [Fact]
    public async Task PatchViews_EditsSavedView()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateSavedViewAsync(
            client,
            new
            {
                name = "Needs edit",
                filter = new { status = "Waiting" }
            });

        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/views/{created.Id}")
        {
            Content = JsonContent.Create(
                new
                {
                    name = "Edited view",
                    filter = new { archive = "Archived", text = "closed" },
                    sort = new { field = "title", direction = "asc" }
                })
        };
        var response = await client.SendAsync(message);
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<SavedViewResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Edited view", updated.Name);
        Assert.Equal("Archived", updated.Filter.Archive);
        Assert.Equal("closed", updated.Filter.Text);
        Assert.Equal("title", updated.Sort.Field);
    }

    [Fact]
    public async Task DeleteViews_SoftDeletesSavedView()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateSavedViewAsync(
            client,
            new
            {
                name = "Temporary view"
            });

        var deleteResponse = await client.DeleteAsync($"/api/views/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var getResponse = await client.GetAsync($"/api/views/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetTasks_FiltersByProject()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var projects = await GetProjectsAsync(client);
        var developmentProject = projects.Single(project => project.Name == "General");
        var jobProjectResponse = await client.PostAsJsonAsync(
            "/api/projects",
            new { name = "Job" });
        jobProjectResponse.EnsureSuccessStatusCode();
        var jobProject = await jobProjectResponse.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(jobProject);

        await AddTaskItemAsync(factory, developmentProject, "Development-only task");
        var jobTask = await AddTaskItemAsync(factory, jobProject, "Job-only task");

        var taskItems = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            $"/api/tasks?projectId={jobProject.Id}");

        Assert.NotNull(taskItems);
        Assert.Contains(taskItems, taskItem => taskItem.Id == jobTask.Id);
        Assert.DoesNotContain(taskItems, taskItem => taskItem.Title == "Development-only task");
    }

    [Fact]
    public async Task GetTasks_FiltersByStatus()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var waiting = await CreateTaskItemAsync(client, "Waiting for input");
        await CreateTaskItemAsync(client, "Ready to move");

        await PatchTaskItemAsync(
            client,
            waiting.Id,
            new { status = "Waiting" });

        var taskItems = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks?status=Waiting");

        Assert.NotNull(taskItems);
        Assert.Contains(taskItems, taskItem => taskItem.Id == waiting.Id);
        Assert.DoesNotContain(taskItems, taskItem => taskItem.Title == "Ready to move");
    }

    [Fact]
    public async Task GetTasks_FiltersArchivedVersusActive()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var active = await CreateTaskItemAsync(client, "Keep active");
        var archived = await CreateTaskItemAsync(client, "Archive this");
        var completedResolution = await GetArchiveResolutionIdAsync(client, "Completed");

        await client.PostAsJsonAsync(
            $"/api/tasks/{archived.Id}/archive",
            new
            {
                archiveResolutionId = completedResolution,
                note = "Done."
            });

        var archivedTaskItems = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks?archive=Archived");
        var activeTaskItems = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks?archive=Active");

        Assert.NotNull(archivedTaskItems);
        Assert.NotNull(activeTaskItems);
        Assert.Contains(archivedTaskItems, taskItem => taskItem.Id == archived.Id);
        Assert.DoesNotContain(archivedTaskItems, taskItem => taskItem.Id == active.Id);
        Assert.Contains(activeTaskItems, taskItem => taskItem.Id == active.Id);
        Assert.DoesNotContain(activeTaskItems, taskItem => taskItem.Id == archived.Id);
    }

    [Fact]
    public async Task GetTasks_FiltersNotViewedSinceDays()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var project = (await GetProjectsAsync(client))
            .Single(candidate => candidate.Name == "General");
        var now = DateTimeOffset.UtcNow;
        var oldTask = await AddTaskItemAsync(
            factory,
            project,
            "Old view",
            now.AddDays(-12),
            viewedAt: now.AddDays(-8));
        await AddTaskItemAsync(
            factory,
            project,
            "Fresh view",
            now.AddDays(-12),
            viewedAt: now.AddDays(-1));

        var taskItems = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks?notViewedSinceDays=7");

        Assert.NotNull(taskItems);
        Assert.Contains(taskItems, taskItem => taskItem.Id == oldTask.Id);
        Assert.DoesNotContain(taskItems, taskItem => taskItem.Title == "Fresh view");
    }

    [Fact]
    public async Task GetTasks_FiltersNotTouchedSinceDays()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var project = (await GetProjectsAsync(client))
            .Single(candidate => candidate.Name == "General");
        var oldTask = await AddTaskItemAsync(
            factory,
            project,
            "Untouched old task",
            DateTimeOffset.UtcNow.AddDays(-20));
        await AddTaskItemAsync(
            factory,
            project,
            "Recently touched task",
            DateTimeOffset.UtcNow.AddDays(-2));

        var taskItems = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks?notTouchedSinceDays=14");

        Assert.NotNull(taskItems);
        Assert.Contains(taskItems, taskItem => taskItem.Id == oldTask.Id);
        Assert.DoesNotContain(taskItems, taskItem => taskItem.Title == "Recently touched task");
    }

    [Fact]
    public async Task GetTaskItemById_UpdatesLastViewedAtWithoutTouchingTask()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "View me");

        var found = await client.GetFromJsonAsync<TaskItemDetailResponse>(
            $"/api/tasks/{created.Id}");

        Assert.NotNull(found);
        Assert.NotNull(found.LastViewedAt);
        Assert.Equal(created.LastTouchedAt, found.LastTouchedAt);
    }

    [Fact]
    public async Task PatchTaskItem_UpdatesLastTouchedAt()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Touch through views");

        await Task.Delay(10);

        var updated = await PatchTaskItemAsync(
            client,
            created.Id,
            new { status = "Waiting" });

        Assert.True(updated.LastTouchedAt > created.LastTouchedAt);
    }

    [Fact]
    public async Task GetTasks_WithViewId_ReturnsExpectedTasks()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var waiting = await CreateTaskItemAsync(client, "View should find me");
        await CreateTaskItemAsync(client, "View should skip me");
        await PatchTaskItemAsync(
            client,
            waiting.Id,
            new { status = "Waiting" });
        var view = await CreateSavedViewAsync(
            client,
            new
            {
                name = "Only waiting",
                filter = new { status = "Waiting" }
            });

        var taskItems = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            $"/api/tasks?viewId={view.Id}");

        Assert.NotNull(taskItems);
        Assert.Contains(taskItems, taskItem => taskItem.Id == waiting.Id);
        Assert.DoesNotContain(taskItems, taskItem => taskItem.Title == "View should skip me");
    }

    private static async Task<SavedViewResponse> CreateSavedViewAsync(
        HttpClient client,
        object request)
    {
        var response = await client.PostAsJsonAsync("/api/views", request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created, got {response.StatusCode}. Body: {body}");

        var created = await response.Content.ReadFromJsonAsync<SavedViewResponse>();
        Assert.NotNull(created);

        return created;
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

    private static async Task<IReadOnlyList<ProjectResponse>> GetProjectsAsync(HttpClient client)
    {
        var projects = await client.GetFromJsonAsync<List<ProjectResponse>>("/api/projects");
        Assert.NotNull(projects);

        return projects;
    }

    private static async Task<Guid> GetArchiveResolutionIdAsync(
        HttpClient client,
        string name)
    {
        var resolutions = await client.GetFromJsonAsync<List<ArchiveResolutionDto>>(
            "/api/archive-resolutions");
        Assert.NotNull(resolutions);

        return resolutions.Single(resolution => resolution.Name == name).Id;
    }

    private static async Task<TaskItem> AddTaskItemAsync(
        DumpTetherApiFactory factory,
        ProjectResponse project,
        string title,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? viewedAt = null)
    {
        var taskItem = TaskItem.Create(
            project.WorkspaceId,
            project.Id,
            title,
            createdAt ?? DateTimeOffset.UtcNow);

        if (viewedAt.HasValue)
        {
            taskItem.MarkViewed(viewedAt.Value);
        }

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        dbContext.TaskItems.Add(taskItem);
        await dbContext.SaveChangesAsync();
        dbContext.Entry(taskItem).State = EntityState.Detached;

        return taskItem;
    }

    private sealed record ArchiveResolutionDto(
        Guid Id,
        string Name);
}
