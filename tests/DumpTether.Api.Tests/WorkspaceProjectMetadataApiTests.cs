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
    public async Task PatchProject_UpdatesColor()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var projects = await client.GetFromJsonAsync<List<ProjectResponse>>("/api/projects");
        var project = Assert.Single(projects!, candidate => candidate.Name == "Development Project");

        var response = await client.PatchAsJsonAsync(
            $"/api/projects/{project.Id}",
            new { color = "#86EFAC" });

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<ProjectResponse>();

        Assert.NotNull(updated);
        Assert.Equal("#86EFAC", updated.Color);
    }
}
