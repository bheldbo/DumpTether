using System.Net;
using System.Net.Http.Json;
using DumpTether.App.Projects;
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
}
