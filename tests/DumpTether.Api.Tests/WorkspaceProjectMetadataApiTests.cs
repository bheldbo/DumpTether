using System.Net;
using System.Net.Http.Json;
using DumpTether.App.Projects;
using DumpTether.App.Tasks;
using DumpTether.App.Workspaces;
using Xunit;

namespace DumpTether.Api.Tests;

public sealed class WorkspaceProjectMetadataApiTests
{
    [Fact]
    public async Task PatchWorkspace_UpdatesColor()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            "/api/workspace",
            new { color = "#93C5FD" });

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();

        Assert.NotNull(updated);
        Assert.Equal("#93C5FD", updated.Color);
    }

    [Fact]
    public async Task PatchWorkspace_RejectsInvalidColor()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            "/api/workspace",
            new { color = "background: red" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostWorkspace_CreatesSelectableWorkspace()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/workspaces",
            new { name = "Travel", color = "#FDE68A" });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
        Assert.NotNull(created);

        client.DefaultRequestHeaders.Add("X-DumpTether-Workspace-Id", created.Id.ToString());
        var current = await client.GetFromJsonAsync<WorkspaceResponse>("/api/workspace");
        var projects = await client.GetFromJsonAsync<List<ProjectResponse>>("/api/projects");

        Assert.Equal(created.Id, current!.Id);
        Assert.Contains(projects!, project => project.Name == "General");
    }

    [Fact]
    public async Task PostWorkspace_RejectsDuplicateName()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var firstResponse = await client.PostAsJsonAsync(
            "/api/workspaces",
            new { name = "Travel" });
        firstResponse.EnsureSuccessStatusCode();
        var duplicateResponse = await client.PostAsJsonAsync(
            "/api/workspaces",
            new { name = "travel" });

        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task PatchProject_UpdatesColor()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var projects = await client.GetFromJsonAsync<List<ProjectResponse>>("/api/projects");
        var project = Assert.Single(projects!, candidate => candidate.Name == "General");

        var response = await client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}",
            new { color = "#86EFAC" });

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<ProjectResponse>();

        Assert.NotNull(updated);
        Assert.Equal("#86EFAC", updated.Color);
    }

    [Fact]
    public async Task PostProject_CreatesProjectTagInCurrentWorkspace()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var workspaceResponse = await client.PostAsJsonAsync(
            "/api/workspaces",
            new { name = "Job" });
        workspaceResponse.EnsureSuccessStatusCode();
        var workspace = await workspaceResponse.Content.ReadFromJsonAsync<WorkspaceResponse>();
        client.DefaultRequestHeaders.Add("X-DumpTether-Workspace-Id", workspace!.Id.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/projects",
            new { name = "Procurement", color = "#93C5FD" });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ProjectResponse>();

        Assert.NotNull(created);
        Assert.Equal(workspace.Id, created.WorkspaceId);
        Assert.Equal("Procurement", created.Name);
        Assert.Equal("#93C5FD", created.Color);
    }

    [Fact]
    public async Task PostProject_RejectsDuplicateName()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();

        var firstResponse = await client.PostAsJsonAsync(
            "/api/projects",
            new { name = "Procurement" });
        firstResponse.EnsureSuccessStatusCode();
        var duplicateResponse = await client.PostAsJsonAsync(
            "/api/projects",
            new { name = "procurement" });

        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteProject_ClearsCategoryWithoutArchivingTasks()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var project = await CreateProjectAsync(client, "Ideas");
        var task = await CreateTaskAsync(client, "Keep this task", project);

        var response = await client.DeleteAsync($"/api/projects/{project.Id}");

        response.EnsureSuccessStatusCode();
        var projects = await client.GetFromJsonAsync<List<ProjectResponse>>("/api/projects");
        var activeTasks = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks");
        var archivedTasks = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks?archive=Archived");

        var updatedTask = Assert.Single(activeTasks!, candidate => candidate.Id == task.Id);
        Assert.DoesNotContain(projects!, candidate => candidate.Id == project.Id);
        Assert.Null(updatedTask.ProjectId);
        Assert.Null(updatedTask.Category);
        Assert.DoesNotContain(archivedTasks!, candidate => candidate.Id == task.Id);
    }

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/projects", new { name });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProjectResponse>())!;
    }

    private static async Task<TaskItemDetailResponse> CreateTaskAsync(
        HttpClient client,
        string title,
        ProjectResponse project)
    {
        var response = await client.PostAsJsonAsync(
            "/api/tasks",
            new
            {
                title,
                projectId = project.Id,
                category = project.Name
            });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>())!;
    }

}
