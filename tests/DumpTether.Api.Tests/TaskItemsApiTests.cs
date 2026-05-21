using System.Net;
using System.Net.Http.Json;
using DumpTether.App.Tasks;
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
}
