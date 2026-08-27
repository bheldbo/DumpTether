using System.Net;
using System.Net.Http.Json;
using DumpTether.App.Auth;
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
        Assert.Null(created.ProjectId);
        Assert.Equal("Capture API notes", created.Title);
        Assert.Equal(created.CreatedAt, created.LastTouchedAt);
    }

    [Fact]
    public async Task PostTaskItems_WithSameClientGeneratedId_IsIdempotent()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var clientGeneratedId = Guid.NewGuid();
        var request = new CreateTaskItemRequest(
            "Retry-safe task",
            ClientGeneratedId: clientGeneratedId);

        var firstResponse = await client.PostAsJsonAsync("/api/tasks", request);
        var secondResponse = await client.PostAsJsonAsync("/api/tasks", request);

        firstResponse.EnsureSuccessStatusCode();
        secondResponse.EnsureSuccessStatusCode();
        var first = (await firstResponse.Content.ReadFromJsonAsync<TaskItemDetailResponse>())!;
        var second = (await secondResponse.Content.ReadFromJsonAsync<TaskItemDetailResponse>())!;
        var tasks = (await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks"))!;

        Assert.Equal(clientGeneratedId, first.Id);
        Assert.Equal(first.Id, second.Id);
        Assert.Single(tasks, task => task.Id == clientGeneratedId);
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
    public async Task Subtasks_CreateAndListWithoutClutteringBoardWall()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var parent = await CreateTaskItemAsync(client, "Prepare release");

        var createResponse = await client.PostAsJsonAsync(
            $"/api/tasks/{parent.Id}/subtasks",
            new CreateTaskItemRequest("Write release notes"));
        createResponse.EnsureSuccessStatusCode();
        var child = (await createResponse.Content.ReadFromJsonAsync<TaskItemDetailResponse>())!;
        var boardTasks = (await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks"))!;
        var subtasks = (await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            $"/api/tasks/{parent.Id}/subtasks"))!;
        var refreshedParent = (await client.GetFromJsonAsync<TaskItemDetailResponse>(
            $"/api/tasks/{parent.Id}"))!;

        Assert.Equal(parent.Id, child.ParentTaskItemId);
        Assert.DoesNotContain(boardTasks, task => task.Id == child.Id);
        Assert.Contains(boardTasks, task => task.Id == parent.Id);
        Assert.Single(subtasks, task => task.Id == child.Id);
        Assert.Equal(1, refreshedParent.SubtaskCount);
        Assert.True(refreshedParent.LastTouchedAt >= parent.LastTouchedAt);
    }

    [Fact]
    public async Task Subtasks_PreviewOnParentAndDeletePermanently()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var parent = await CreateTaskItemAsync(client, "Parent with notes");
        var childIds = new List<Guid>();

        for (var index = 1; index <= 4; index++)
        {
            var createResponse = await client.PostAsJsonAsync(
                $"/api/tasks/{parent.Id}/subtasks",
                new CreateTaskItemRequest($"Child {index}"));
            createResponse.EnsureSuccessStatusCode();
            childIds.Add((await createResponse.Content.ReadFromJsonAsync<TaskItemDetailResponse>())!.Id);
        }

        var archiveResponse = await client.PostAsync(
            $"/api/tasks/{childIds[0]}/archive",
            content: null);
        Assert.Equal(HttpStatusCode.BadRequest, archiveResponse.StatusCode);
        var boardTasks = (await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks"))!;
        var parentSummary = Assert.Single(boardTasks, task => task.Id == parent.Id);
        Assert.Equal(4, parentSummary.SubtaskCount);
        Assert.Equal(3, parentSummary.SubtaskPreviews!.Count);

        var deleteResponse = await client.DeleteAsync(
            $"/api/tasks/{parent.Id}/subtasks/{childIds[0]}");
        deleteResponse.EnsureSuccessStatusCode();
        var updatedParent = (await deleteResponse.Content.ReadFromJsonAsync<TaskItemDetailResponse>())!;

        Assert.Equal(3, updatedParent.SubtaskCount);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/tasks/{childIds[0]}")).StatusCode);
        Assert.Contains(updatedParent.TimelineEntries, entry =>
            entry.Summary == "Subtask deleted permanently" && entry.Details == "Child 1");
        var remaining = (await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            $"/api/tasks/{parent.Id}/subtasks"))!;
        Assert.Equal(3, remaining.Count);
    }
    [Fact]
    public async Task Subtasks_RejectNestedChildren()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var parent = await CreateTaskItemAsync(client, "Parent");
        var childResponse = await client.PostAsJsonAsync(
            $"/api/tasks/{parent.Id}/subtasks",
            new CreateTaskItemRequest("Child"));
        childResponse.EnsureSuccessStatusCode();
        var child = (await childResponse.Content.ReadFromJsonAsync<TaskItemDetailResponse>())!;

        var response = await client.PostAsJsonAsync(
            $"/api/tasks/{child.Id}/subtasks",
            new CreateTaskItemRequest("Grandchild"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CopyTaskItems_WithParentSelected_IncludesChildAndPreservesHierarchy()
    {
        using var factory = new DumpTetherApiFactory(
            requireAuthentication: true,
            environmentName: "Desktop");
        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsync("/api/auth/local-desktop", content: null);
        loginResponse.EnsureSuccessStatusCode();
        var login = (await loginResponse.Content.ReadFromJsonAsync<LoginUserResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.SessionToken);
        var parent = await CreateTaskItemAsync(client, "Release checklist");
        var childResponse = await client.PostAsJsonAsync(
            $"/api/tasks/{parent.Id}/subtasks",
            new CreateTaskItemRequest("Publish packages"));
        childResponse.EnsureSuccessStatusCode();
        var child = (await childResponse.Content.ReadFromJsonAsync<TaskItemDetailResponse>())!;

        var copyResponse = await client.PostAsJsonAsync(
            "/api/tasks/copy",
            new CopyTaskItemsRequest(
                [parent.Id],
                parent.WorkspaceId));
        copyResponse.EnsureSuccessStatusCode();
        var copied = (await copyResponse.Content.ReadFromJsonAsync<CopyTaskItemsResponse>())!.Tasks;
        var copiedParent = Assert.Single(copied, task => task.ParentTaskItemId is null);
        var copiedChild = Assert.Single(copied, task => task.ParentTaskItemId.HasValue);

        Assert.Equal(copiedParent.Id, copiedChild.ParentTaskItemId);
        Assert.Equal(parent.Title, copiedParent.Title);
        Assert.Equal(child.Title, copiedChild.Title);
    }

    [Fact]
    public async Task Subtasks_RejectReusingClientGeneratedIdWithDifferentParent()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var firstParent = await CreateTaskItemAsync(client, "First parent");
        var secondParent = await CreateTaskItemAsync(client, "Second parent");
        var clientGeneratedId = Guid.NewGuid();
        var firstResponse = await client.PostAsJsonAsync(
            $"/api/tasks/{firstParent.Id}/subtasks",
            new CreateTaskItemRequest("Stable child", ClientGeneratedId: clientGeneratedId));
        firstResponse.EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync(
            $"/api/tasks/{secondParent.Id}/subtasks",
            new CreateTaskItemRequest("Stable child", ClientGeneratedId: clientGeneratedId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
    public async Task PostTaskItems_WhenWorkspaceActiveTaskLimitReached_ReturnsBadRequest()
    {
        using var factory = new DumpTetherApiFactory(maxActiveTasksPerWorkspace: 1);
        using var client = factory.CreateClient();
        await CreateTaskItemAsync(client, "First task");

        var response = await client.PostAsJsonAsync(
            "/api/tasks",
            new { title = "Second task" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
    public async Task PostTaskTimeline_WithSameClientGeneratedId_IsIdempotent()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Retry-safe note");
        var clientGeneratedId = Guid.NewGuid();
        var request = new AddTaskTimelineEntryRequest(
            "Only once.",
            ClientGeneratedId: clientGeneratedId);

        var firstResponse = await client.PostAsJsonAsync(
            $"/api/tasks/{created.Id}/timeline",
            request);
        var secondResponse = await client.PostAsJsonAsync(
            $"/api/tasks/{created.Id}/timeline",
            request);

        firstResponse.EnsureSuccessStatusCode();
        secondResponse.EnsureSuccessStatusCode();
        var detail = (await secondResponse.Content.ReadFromJsonAsync<TaskItemDetailResponse>())!;

        var note = Assert.Single(
            detail.TimelineEntries,
            entry => entry.Id == clientGeneratedId);
        Assert.Equal("Only once.", note.Details);
    }

    [Fact]
    public async Task PostTaskArchive_ArchivesTaskItemWithoutRequestBody()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Archive me");
        var archived = await PostArchiveAsync(client, created.Id);

        Assert.NotNull(archived.ArchivedAt);

        var entry = archived.TimelineEntries.Last();
        Assert.Equal("Archived", entry.Kind);
        Assert.Equal("Task item archived", entry.Summary);
        Assert.Null(entry.Details);
    }

    [Fact]
    public async Task PostTaskArchive_WhenAlreadyArchived_ReturnsBadRequest()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Archive once");
        await PostArchiveAsync(client, created.Id);

        var response = await client.PostAsync($"/api/tasks/{created.Id}/archive", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTaskItems_ExcludesArchivedTaskItemsByDefault()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Hide when archived");
        await PostArchiveAsync(client, created.Id);

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
        await PostArchiveAsync(client, created.Id);

        var taskItems = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks?scope=Archive");

        Assert.NotNull(taskItems);
        Assert.Contains(taskItems, taskItem => taskItem.Id == created.Id);
    }

    [Fact]
    public async Task PostTaskReopen_ReopensArchivedTaskItem()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Reopen me");
        await PostArchiveAsync(client, created.Id);

        var reopened = await PostReopenAsync(
            client,
            created.Id,
            new
            {
                note = "Needs another pass."
            });

        Assert.Null(reopened.ArchivedAt);

        var entry = reopened.TimelineEntries.Last();
        Assert.Equal("Reopened", entry.Kind);
        Assert.Equal("Task item reopened", entry.Summary);
        Assert.Equal("Needs another pass.", entry.Details);

        var taskItems = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks");
        Assert.NotNull(taskItems);
        Assert.Contains(taskItems, taskItem => taskItem.Id == created.Id);
    }

    [Fact]
    public async Task PostTaskReopenMany_ReopensSelectedArchivedTaskItems()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var first = await CreateTaskItemAsync(client, "Batch reopen first");
        var second = await CreateTaskItemAsync(client, "Batch reopen second");
        await PostArchiveAsync(client, first.Id);
        await PostArchiveAsync(client, second.Id);

        var response = await client.PostAsJsonAsync(
            "/api/tasks/reopen",
            new
            {
                taskItemIds = new[] { first.Id, second.Id },
                note = "Back on the wall."
            });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TaskItemBatchResponse>();

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        var activeTasks = await client.GetFromJsonAsync<List<TaskItemSummaryResponse>>("/api/tasks");
        Assert.NotNull(activeTasks);
        Assert.Contains(activeTasks, task => task.Id == first.Id);
        Assert.Contains(activeTasks, task => task.Id == second.Id);
    }

    [Fact]
    public async Task PostTaskPermanentDelete_RemovesArchivedTaskItems()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateTaskItemAsync(client, "Delete from archive");
        await PostArchiveAsync(client, created.Id);

        var response = await client.PostAsJsonAsync(
            "/api/tasks/permanent-delete",
            new
            {
                taskItemIds = new[] { created.Id }
            });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TaskItemBatchResponse>();

        Assert.NotNull(result);
        Assert.Equal(1, result.Count);

        var fetchResponse = await client.GetAsync($"/api/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, fetchResponse.StatusCode);
    }

    [Fact]
    public async Task PostTaskPermanentDelete_ExpandsArchivedChildrenButRejectsActiveChildren()
    {
        using var factory = new DumpTetherApiFactory();
        using var client = factory.CreateClient();
        var parent = await CreateTaskItemAsync(client, "Archived parent");
        var childResponse = await client.PostAsJsonAsync(
            $"/api/tasks/{parent.Id}/subtasks",
            new CreateTaskItemRequest("Child lifecycle"));
        childResponse.EnsureSuccessStatusCode();
        var child = (await childResponse.Content.ReadFromJsonAsync<TaskItemDetailResponse>())!;
        await PostArchiveAsync(client, parent.Id);

        var rejected = await client.PostAsJsonAsync(
            "/api/tasks/permanent-delete",
            new { taskItemIds = new[] { parent.Id } });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var childDeleted = await client.DeleteAsync(
            $"/api/tasks/{parent.Id}/subtasks/{child.Id}");
        childDeleted.EnsureSuccessStatusCode();
        var deleted = await client.PostAsJsonAsync(
            "/api/tasks/permanent-delete",
            new { taskItemIds = new[] { parent.Id } });
        deleted.EnsureSuccessStatusCode();
        var result = await deleted.Content.ReadFromJsonAsync<TaskItemBatchResponse>();

        Assert.NotNull(result);
        Assert.Equal(1, result.Count);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tasks/{parent.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tasks/{child.Id}")).StatusCode);
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
        Guid id)
    {
        var response = await client.PostAsync($"/api/tasks/{id}/archive", content: null);
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

}
