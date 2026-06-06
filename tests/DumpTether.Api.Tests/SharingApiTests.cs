using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DumpTether.App.Auth;
using DumpTether.App.LiveUpdates;
using DumpTether.App.Templates;
using DumpTether.App.Tasks;
using DumpTether.App.Workspaces;
using DumpTether.Data;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DumpTether.Api.Tests;

public sealed class SharingApiTests
{
    [Fact]
    public async Task WorkspaceInvitation_AcceptedUserCanSeeWorkspaceTasks()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var invitedClient = factory.CreateClient();
        var owner = await RegisterAndLoginAsync(
            ownerClient,
            "owner@example.com",
            "correct horse battery");
        var invited = await RegisterAndLoginAsync(
            invitedClient,
            "invited@example.com",
            "correct horse battery");
        var ownerWorkspaceId = owner.Workspaces.Single().Id;
        var ownerTask = await CreateTaskAsync(ownerClient, "Owner task visible after tavle invite");

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            "/api/workspace/invitations",
            new
            {
                email = invited.User.Email
            });
        inviteResponse.EnsureSuccessStatusCode();
        var invite = await inviteResponse.Content.ReadFromJsonAsync<WorkspaceInvitationResponse>();

        Assert.NotNull(invite);
        Assert.False(string.IsNullOrWhiteSpace(invite!.Token));

        var acceptResponse = await invitedClient.PostAsJsonAsync(
            "/api/workspace/invitations/accept",
            new
            {
                token = invite.Token
            });
        acceptResponse.EnsureSuccessStatusCode();

        SetWorkspaceHeader(invitedClient, ownerWorkspaceId);
        var invitedTasks = await invitedClient.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks");
        var invitedMembers = await invitedClient.GetFromJsonAsync<List<WorkspaceMemberResponse>>(
            "/api/workspace/members");

        Assert.Contains(invitedTasks!, taskItem => taskItem.Id == ownerTask.Id);
        Assert.Contains(invitedMembers!, member => member.Email == invited.User.Email);
    }

    [Fact]
    public async Task LiveUpdates_SharedWorkspaceMemberReceivesTaskCreatedEvent()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await RegisterAndLoginAsync(
            ownerClient,
            "live-owner@example.com",
            "correct horse battery");
        var member = await RegisterAndLoginAsync(
            memberClient,
            "live-member@example.com",
            "correct horse battery");
        var ownerWorkspaceId = owner.Workspaces.Single().Id;

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            "/api/workspace/invitations",
            new
            {
                email = member.User.Email
            });
        inviteResponse.EnsureSuccessStatusCode();
        var invite = (await inviteResponse.Content.ReadFromJsonAsync<WorkspaceInvitationResponse>())!;

        var acceptResponse = await memberClient.PostAsJsonAsync(
            "/api/workspace/invitations/accept",
            new
            {
                token = invite.Token
            });
        acceptResponse.EnsureSuccessStatusCode();

        var received = new TaskCompletionSource<LiveUpdateMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress, "/api/live"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    options.AccessTokenProvider = () => Task.FromResult<string?>(member.SessionToken);
                    options.Headers.Add("Authorization", $"Bearer {member.SessionToken}");
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();

        connection.On<LiveUpdateMessage>("LiveUpdate", message =>
        {
            if (message.EventName == LiveUpdateEvents.TaskCreated)
            {
                received.TrySetResult(message);
            }
        });
        await connection.StartAsync();
        await connection.InvokeAsync("JoinWorkspace", ownerWorkspaceId);

        var created = await CreateTaskAsync(ownerClient, "Live task from owner");
        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(LiveUpdateEvents.TaskCreated, message.EventName);
        Assert.Equal(ownerWorkspaceId, message.WorkspaceId);
        Assert.Equal(created.Id, message.TaskItemId);
        Assert.Equal(owner.User.Id, message.ActorUserId);
    }

    [Fact]
    public async Task LiveUpdates_WithoutSession_WhenAuthRequired_CannotConnect()
    {
        using var factory = new DumpTetherApiFactory(requireAuthentication: true);
        await using var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress, "/api/live"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task LiveUpdates_RevokedSession_WhenAuthRequired_CannotConnect()
    {
        using var factory = new DumpTetherApiFactory(requireAuthentication: true);
        using var client = factory.CreateClient();
        var login = await RegisterAndLoginAsync(
            client,
            "live-revoked@example.com",
            "correct horse battery");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
            var session = await dbContext.UserSessions.SingleAsync(candidate =>
                candidate.UserId == login.User.Id);
            session.Revoke(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync();
        }

        await using var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress, "/api/live"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    options.AccessTokenProvider = () => Task.FromResult<string?>(login.SessionToken);
                    options.Headers.Add("Authorization", $"Bearer {login.SessionToken}");
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task WorkspaceInvitation_CreateAcceptsStringRole()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var invitedClient = factory.CreateClient();
        await RegisterAndLoginAsync(
            ownerClient,
            "string-role-owner@example.com",
            "correct horse battery");
        var invited = await RegisterAndLoginAsync(
            invitedClient,
            "string-role-invited@example.com",
            "correct horse battery");

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            "/api/workspace/invitations",
            new
            {
                email = invited.User.Email,
                role = "Member"
            });

        var body = await inviteResponse.Content.ReadAsStringAsync();
        Assert.True(
            inviteResponse.StatusCode == HttpStatusCode.Created,
            $"Expected Created, got {inviteResponse.StatusCode}. Body: {body}");
    }

    [Fact]
    public async Task WorkspaceInvitation_IncomingInboxCanAcceptById()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var invitedClient = factory.CreateClient();
        var owner = await RegisterAndLoginAsync(
            ownerClient,
            "inbox-owner@example.com",
            "correct horse battery");
        var invited = await RegisterAndLoginAsync(
            invitedClient,
            "inbox-invited@example.com",
            "correct horse battery");
        var ownerWorkspaceId = owner.Workspaces.Single().Id;

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            "/api/workspace/invitations",
            new
            {
                email = invited.User.Email
            });
        inviteResponse.EnsureSuccessStatusCode();

        var inbox = await GetRequiredJsonAsync<List<WorkspaceInvitationInboxResponse>>(
            invitedClient,
            "/api/account/invitations");

        Assert.NotNull(inbox);
        var invitation = Assert.Single(inbox!);
        Assert.Equal(ownerWorkspaceId, invitation.WorkspaceId);
        Assert.Equal(owner.User.Email, invitation.InvitedByEmail);

        var acceptResponse = await invitedClient.PostAsync(
            $"/api/account/invitations/{invitation.Id}/accept",
            content: null);
        acceptResponse.EnsureSuccessStatusCode();

        var workspaces = await GetRequiredJsonAsync<List<WorkspaceResponse>>(
            invitedClient,
            "/api/workspaces");
        var remainingInvitations = await GetRequiredJsonAsync<List<WorkspaceInvitationInboxResponse>>(
            invitedClient,
            "/api/account/invitations");

        Assert.Contains(workspaces!, workspace => workspace.Id == ownerWorkspaceId);
        Assert.Empty(remainingInvitations!);
    }

    [Fact]
    public async Task WorkspaceInvitation_IncomingInboxCanDecline()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var invitedClient = factory.CreateClient();
        var owner = await RegisterAndLoginAsync(
            ownerClient,
            "decline-owner@example.com",
            "correct horse battery");
        var invited = await RegisterAndLoginAsync(
            invitedClient,
            "decline-invited@example.com",
            "correct horse battery");
        var ownerWorkspaceId = owner.Workspaces.Single().Id;

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            "/api/workspace/invitations",
            new
            {
                email = invited.User.Email
            });
        inviteResponse.EnsureSuccessStatusCode();

        var inbox = await GetRequiredJsonAsync<List<WorkspaceInvitationInboxResponse>>(
            invitedClient,
            "/api/account/invitations");
        var invitation = Assert.Single(inbox!);

        var declineResponse = await invitedClient.DeleteAsync(
            $"/api/account/invitations/{invitation.Id}");
        declineResponse.EnsureSuccessStatusCode();

        var workspaces = await GetRequiredJsonAsync<List<WorkspaceResponse>>(
            invitedClient,
            "/api/workspaces");
        var remainingInvitations = await GetRequiredJsonAsync<List<WorkspaceInvitationInboxResponse>>(
            invitedClient,
            "/api/account/invitations");

        Assert.DoesNotContain(workspaces!, workspace => workspace.Id == ownerWorkspaceId);
        Assert.Empty(remainingInvitations!);
    }

    [Fact]
    public async Task WorkspaceMember_CanLeaveSharedBoard()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await RegisterAndLoginAsync(
            ownerClient,
            "leave-owner@example.com",
            "correct horse battery");
        var member = await RegisterAndLoginAsync(
            memberClient,
            "leave-member@example.com",
            "correct horse battery");
        var ownerWorkspaceId = owner.Workspaces.Single().Id;

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            "/api/workspace/invitations",
            new
            {
                email = member.User.Email
            });
        inviteResponse.EnsureSuccessStatusCode();
        var invite = await inviteResponse.Content.ReadFromJsonAsync<WorkspaceInvitationResponse>();
        var acceptResponse = await memberClient.PostAsJsonAsync(
            "/api/workspace/invitations/accept",
            new
            {
                token = invite!.Token
            });
        acceptResponse.EnsureSuccessStatusCode();

        SetWorkspaceHeader(memberClient, ownerWorkspaceId);
        var leaveResponse = await memberClient.DeleteAsync("/api/workspace/membership");
        leaveResponse.EnsureSuccessStatusCode();

        var workspaces = await GetRequiredJsonAsync<List<WorkspaceResponse>>(
            memberClient,
            "/api/workspaces");

        Assert.DoesNotContain(workspaces!, workspace => workspace.Id == ownerWorkspaceId);
    }

    [Fact]
    public async Task WorkspaceMember_CanCreateTaskInSharedBoard()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await RegisterAndLoginAsync(
            ownerClient,
            "shared-create-owner@example.com",
            "correct horse battery");
        var member = await RegisterAndLoginAsync(
            memberClient,
            "shared-create-member@example.com",
            "correct horse battery");
        var ownerWorkspaceId = owner.Workspaces.Single().Id;

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            "/api/workspace/invitations",
            new
            {
                email = member.User.Email
            });
        inviteResponse.EnsureSuccessStatusCode();
        var invite = await inviteResponse.Content.ReadFromJsonAsync<WorkspaceInvitationResponse>();
        var acceptResponse = await memberClient.PostAsJsonAsync(
            "/api/workspace/invitations/accept",
            new
            {
                token = invite!.Token
            });
        acceptResponse.EnsureSuccessStatusCode();

        SetWorkspaceHeader(memberClient, ownerWorkspaceId);
        var createResponse = await memberClient.PostAsJsonAsync(
            "/api/tasks",
            new
            {
                title = "Created by shared board member"
            });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<TaskItemDetailResponse>();

        SetWorkspaceHeader(ownerClient, ownerWorkspaceId);
        var ownerTasks = await ownerClient.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks");

        Assert.Contains(ownerTasks!, taskItem => taskItem.Id == created!.Id);
    }

    [Fact]
    public async Task WorkspaceMember_ReadOnlyCanReadButCannotWriteSharedBoard()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var readOnlyClient = factory.CreateClient();
        var owner = await RegisterAndLoginAsync(
            ownerClient,
            "readonly-owner@example.com",
            "correct horse battery");
        var readOnlyUser = await RegisterAndLoginAsync(
            readOnlyClient,
            "readonly-member@example.com",
            "correct horse battery");
        var ownerWorkspaceId = owner.Workspaces.Single().Id;
        var ownerTask = await CreateTaskAsync(ownerClient, "Read-only visible task");

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            "/api/workspace/invitations",
            new
            {
                email = readOnlyUser.User.Email,
                role = "ReadOnly"
            });
        inviteResponse.EnsureSuccessStatusCode();
        var invite = await inviteResponse.Content.ReadFromJsonAsync<WorkspaceInvitationResponse>();
        var acceptResponse = await readOnlyClient.PostAsJsonAsync(
            "/api/workspace/invitations/accept",
            new
            {
                token = invite!.Token
            });
        acceptResponse.EnsureSuccessStatusCode();

        SetWorkspaceHeader(readOnlyClient, ownerWorkspaceId);
        var visibleTasks = await GetRequiredJsonAsync<List<TaskItemSummaryResponse>>(
            readOnlyClient,
            "/api/tasks");
        var currentUser = await GetRequiredJsonAsync<CurrentUserResponse>(
            readOnlyClient,
            "/api/auth/me");
        var createTaskResponse = await readOnlyClient.PostAsJsonAsync(
            "/api/tasks",
            new
            {
                title = "Should not be created"
            });
        var updateTaskResponse = await readOnlyClient.PatchAsJsonAsync(
            $"/api/tasks/{ownerTask.Id}",
            new
            {
                title = "Should not be updated"
            });
        var createCategoryResponse = await readOnlyClient.PostAsJsonAsync(
            "/api/projects",
            new
            {
                name = "Should not be created"
            });

        var visibleWorkspace = Assert.Single(
            currentUser.Workspaces,
            workspace => workspace.Id == ownerWorkspaceId);
        Assert.Equal("ReadOnly", visibleWorkspace.Role.ToString());
        Assert.Contains(visibleTasks!, taskItem => taskItem.Id == ownerTask.Id);
        Assert.Equal(HttpStatusCode.BadRequest, createTaskResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, updateTaskResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createCategoryResponse.StatusCode);
    }

    [Fact]
    public async Task Workspace_OwnerCanDeleteBoardAndScopedData()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        await RegisterAndLoginAsync(
            ownerClient,
            "delete-board-owner@example.com",
            "correct horse battery");
        var workspace = await CreateWorkspaceAsync(ownerClient, "Delete me");
        SetWorkspaceHeader(ownerClient, workspace.Id);
        var task = await CreateTaskAsync(ownerClient, "Board delete task");

        var noteResponse = await ownerClient.PostAsJsonAsync(
            $"/api/tasks/{task.Id}/timeline",
            new
            {
                note = "This note should be deleted with the board."
            });
        noteResponse.EnsureSuccessStatusCode();

        var deleteResponse = await ownerClient.DeleteAsync($"/api/workspaces/{workspace.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();

        Assert.False(await dbContext.Workspaces.AnyAsync(candidate => candidate.Id == workspace.Id));
        Assert.False(await dbContext.TaskItems.AnyAsync(taskItem => taskItem.WorkspaceId == workspace.Id));
        Assert.False(await dbContext.TaskTimelineEntries.AnyAsync(entry => entry.TaskItemId == task.Id));
        Assert.False(await dbContext.ArchiveResolutions.AnyAsync(reason => reason.WorkspaceId == workspace.Id));
        Assert.False(await dbContext.Projects.AnyAsync(project => project.WorkspaceId == workspace.Id));
    }

    [Fact]
    public async Task Workspace_MemberCannotDeleteSharedBoard()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await RegisterAndLoginAsync(
            ownerClient,
            "member-delete-owner@example.com",
            "correct horse battery");
        var member = await RegisterAndLoginAsync(
            memberClient,
            "member-delete-user@example.com",
            "correct horse battery");
        var ownerWorkspaceId = owner.Workspaces.Single().Id;

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            "/api/workspace/invitations",
            new
            {
                email = member.User.Email
            });
        inviteResponse.EnsureSuccessStatusCode();
        var invite = await inviteResponse.Content.ReadFromJsonAsync<WorkspaceInvitationResponse>();
        var acceptResponse = await memberClient.PostAsJsonAsync(
            "/api/workspace/invitations/accept",
            new
            {
                token = invite!.Token
            });
        acceptResponse.EnsureSuccessStatusCode();

        SetWorkspaceHeader(memberClient, ownerWorkspaceId);
        var deleteResponse = await memberClient.DeleteAsync($"/api/workspaces/{ownerWorkspaceId}");

        Assert.Equal(HttpStatusCode.Unauthorized, deleteResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();

        Assert.True(await dbContext.Workspaces.AnyAsync(candidate => candidate.Id == ownerWorkspaceId));
    }

    [Fact]
    public async Task TaskShare_SharedOnlyUserSeesOnlySharedTasks()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var sharedClient = factory.CreateClient();
        var owner = await RegisterAndLoginAsync(
            ownerClient,
            "task-owner@example.com",
            "correct horse battery");
        var sharedUser = await RegisterAndLoginAsync(
            sharedClient,
            "task-shared@example.com",
            "correct horse battery");
        var ownerWorkspaceId = owner.Workspaces.Single().Id;
        var sharedTask = await CreateTaskAsync(ownerClient, "Shared task");
        var privateTask = await CreateTaskAsync(ownerClient, "Private task");

        var shareResponse = await ownerClient.PostAsJsonAsync(
            $"/api/tasks/{sharedTask.Id}/shares",
            new
            {
                email = sharedUser.User.Email,
                role = 2
            });
        shareResponse.EnsureSuccessStatusCode();
        var sharedTaskAfterShare = await shareResponse.Content.ReadFromJsonAsync<TaskItemDetailResponse>();

        Assert.NotNull(sharedTaskAfterShare);
        Assert.Contains(
            sharedTaskAfterShare!.Shares,
            share => share.Email == sharedUser.User.Email.ToLowerInvariant());

        SetWorkspaceHeader(sharedClient, ownerWorkspaceId);
        var visibleTasks = await sharedClient.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks");
        var sharedDetail = await sharedClient.GetAsync($"/api/tasks/{sharedTask.Id}");
        var privateDetail = await sharedClient.GetAsync($"/api/tasks/{privateTask.Id}");

        Assert.Contains(visibleTasks!, taskItem => taskItem.Id == sharedTask.Id);
        Assert.DoesNotContain(visibleTasks!, taskItem => taskItem.Id == privateTask.Id);
        sharedDetail.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, privateDetail.StatusCode);
    }

    [Fact]
    public async Task TaskShare_CreateAcceptsStringRole()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var sharedClient = factory.CreateClient();
        await RegisterAndLoginAsync(
            ownerClient,
            "task-string-role-owner@example.com",
            "correct horse battery");
        var sharedUser = await RegisterAndLoginAsync(
            sharedClient,
            "task-string-role-shared@example.com",
            "correct horse battery");
        var sharedTask = await CreateTaskAsync(ownerClient, "Shared task string role");

        var shareResponse = await ownerClient.PostAsJsonAsync(
            $"/api/tasks/{sharedTask.Id}/shares",
            new
            {
                email = sharedUser.User.Email,
                role = "Editor"
            });

        var body = await shareResponse.Content.ReadAsStringAsync();
        Assert.True(
            shareResponse.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {shareResponse.StatusCode}. Body: {body}");
    }

    [Fact]
    public async Task TaskShareLink_CreateReturnsPendingOneTimeLink()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var sharedClient = factory.CreateClient();
        await RegisterAndLoginAsync(
            ownerClient,
            "link-create-owner@example.com",
            "correct horse battery");
        var sharedUser = await RegisterAndLoginAsync(
            sharedClient,
            "link-create-shared@example.com",
            "correct horse battery");
        var sharedTask = await CreateTaskAsync(ownerClient, "Pending link task");

        var linkResponse = await ownerClient.PostAsJsonAsync(
            $"/api/tasks/{sharedTask.Id}/share-links",
            new
            {
                email = sharedUser.User.Email,
                role = "Editor"
            });
        linkResponse.EnsureSuccessStatusCode();
        var link = await linkResponse.Content.ReadFromJsonAsync<TaskShareLinkResponse>();

        Assert.NotNull(link);
        Assert.False(string.IsNullOrWhiteSpace(link!.Token));
        Assert.True(link.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.True(link.ExpiresAt <= DateTimeOffset.UtcNow.AddDays(1).AddMinutes(1));

        var updatedTask = await ownerClient.GetFromJsonAsync<TaskItemDetailResponse>(
            $"/api/tasks/{sharedTask.Id}");
        var share = Assert.Single(updatedTask!.Shares);

        Assert.Equal(sharedUser.User.Email.ToLowerInvariant(), share.Email);
        Assert.Null(share.AcceptedAt);
        Assert.NotNull(share.ExpiresAt);
    }

    [Fact]
    public async Task TaskShareLink_AcceptGrantsSharedTaskAccess()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var sharedClient = factory.CreateClient();
        var owner = await RegisterAndLoginAsync(
            ownerClient,
            "link-accept-owner@example.com",
            "correct horse battery");
        var sharedUser = await RegisterAndLoginAsync(
            sharedClient,
            "link-accept-shared@example.com",
            "correct horse battery");
        var ownerWorkspaceId = owner.Workspaces.Single().Id;
        var sharedTask = await CreateTaskAsync(ownerClient, "Accepted shared link task");
        var privateTask = await CreateTaskAsync(ownerClient, "Still private after link");

        var linkResponse = await ownerClient.PostAsJsonAsync(
            $"/api/tasks/{sharedTask.Id}/share-links",
            new
            {
                email = sharedUser.User.Email
            });
        linkResponse.EnsureSuccessStatusCode();
        var link = (await linkResponse.Content.ReadFromJsonAsync<TaskShareLinkResponse>())!;

        var acceptResponse = await sharedClient.PostAsJsonAsync(
            "/api/share-links/accept",
            new
            {
                token = link.Token
            });
        acceptResponse.EnsureSuccessStatusCode();

        SetWorkspaceHeader(sharedClient, ownerWorkspaceId);
        var visibleTasks = await sharedClient.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks");
        var sharedDetail = await sharedClient.GetAsync($"/api/tasks/{sharedTask.Id}");
        var privateDetail = await sharedClient.GetAsync($"/api/tasks/{privateTask.Id}");

        Assert.Contains(visibleTasks!, taskItem => taskItem.Id == sharedTask.Id);
        Assert.DoesNotContain(visibleTasks!, taskItem => taskItem.Id == privateTask.Id);
        sharedDetail.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, privateDetail.StatusCode);
    }

    [Fact]
    public async Task TaskShareLink_ExpiredLinkCannotBeAccepted()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var sharedClient = factory.CreateClient();
        await RegisterAndLoginAsync(
            ownerClient,
            "link-expired-owner@example.com",
            "correct horse battery");
        var sharedUser = await RegisterAndLoginAsync(
            sharedClient,
            "link-expired-shared@example.com",
            "correct horse battery");
        var sharedTask = await CreateTaskAsync(ownerClient, "Expired shared link task");

        var linkResponse = await ownerClient.PostAsJsonAsync(
            $"/api/tasks/{sharedTask.Id}/share-links",
            new
            {
                email = sharedUser.User.Email
            });
        linkResponse.EnsureSuccessStatusCode();
        var link = (await linkResponse.Content.ReadFromJsonAsync<TaskShareLinkResponse>())!;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
            var share = await dbContext.TaskItemShares.SingleAsync(candidate =>
                candidate.TaskItemId == sharedTask.Id &&
                candidate.NormalizedEmail == sharedUser.User.Email.ToUpperInvariant());
            dbContext.Entry(share).Property(nameof(share.ExpiresAt)).CurrentValue =
                DateTimeOffset.UtcNow.AddMinutes(-1);
            await dbContext.SaveChangesAsync();
        }

        var acceptResponse = await sharedClient.PostAsJsonAsync(
            "/api/share-links/accept",
            new
            {
                token = link.Token
            });

        Assert.Equal(HttpStatusCode.BadRequest, acceptResponse.StatusCode);
    }

    [Fact]
    public async Task TaskShareLink_RevokedLinkCannotBeAccepted()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var sharedClient = factory.CreateClient();
        await RegisterAndLoginAsync(
            ownerClient,
            "link-revoked-owner@example.com",
            "correct horse battery");
        var sharedUser = await RegisterAndLoginAsync(
            sharedClient,
            "link-revoked-shared@example.com",
            "correct horse battery");
        var sharedTask = await CreateTaskAsync(ownerClient, "Revoked shared link task");

        var linkResponse = await ownerClient.PostAsJsonAsync(
            $"/api/tasks/{sharedTask.Id}/share-links",
            new
            {
                email = sharedUser.User.Email
            });
        linkResponse.EnsureSuccessStatusCode();
        var link = (await linkResponse.Content.ReadFromJsonAsync<TaskShareLinkResponse>())!;
        var share = Assert.Single(link.Shares);

        var revokeResponse = await ownerClient.DeleteAsync(
            $"/api/tasks/{sharedTask.Id}/shares/{share.Id}");
        revokeResponse.EnsureSuccessStatusCode();

        var acceptResponse = await sharedClient.PostAsJsonAsync(
            "/api/share-links/accept",
            new
            {
                token = link.Token
            });

        Assert.Equal(HttpStatusCode.BadRequest, acceptResponse.StatusCode);
    }

    [Fact]
    public async Task TaskShare_SharedOnlyUserCannotCreateTaskInOwnersWorkspace()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var sharedClient = factory.CreateClient();
        var owner = await RegisterAndLoginAsync(
            ownerClient,
            "create-owner@example.com",
            "correct horse battery");
        var sharedUser = await RegisterAndLoginAsync(
            sharedClient,
            "create-shared@example.com",
            "correct horse battery");
        var ownerWorkspaceId = owner.Workspaces.Single().Id;
        var sharedTask = await CreateTaskAsync(ownerClient, "Shared create guard task");

        var shareResponse = await ownerClient.PostAsJsonAsync(
            $"/api/tasks/{sharedTask.Id}/share-links",
            new
            {
                email = sharedUser.User.Email
            });
        shareResponse.EnsureSuccessStatusCode();
        var link = (await shareResponse.Content.ReadFromJsonAsync<TaskShareLinkResponse>())!;
        var acceptResponse = await sharedClient.PostAsJsonAsync(
            "/api/share-links/accept",
            new
            {
                token = link.Token
            });
        acceptResponse.EnsureSuccessStatusCode();

        SetWorkspaceHeader(sharedClient, ownerWorkspaceId);
        var createResponse = await sharedClient.PostAsJsonAsync(
            "/api/tasks",
            new
            {
                title = "Should not be created by task-share access"
            });

        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
    }

    [Fact]
    public async Task TaskShare_IncomingInboxCanLeaveShare()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var sharedClient = factory.CreateClient();
        var owner = await RegisterAndLoginAsync(
            ownerClient,
            "share-inbox-owner@example.com",
            "correct horse battery");
        var sharedUser = await RegisterAndLoginAsync(
            sharedClient,
            "share-inbox-user@example.com",
            "correct horse battery");
        var ownerWorkspaceId = owner.Workspaces.Single().Id;
        var sharedTask = await CreateTaskAsync(ownerClient, "Incoming share task");

        var shareResponse = await ownerClient.PostAsJsonAsync(
            $"/api/tasks/{sharedTask.Id}/share-links",
            new
            {
                email = sharedUser.User.Email
            });
        shareResponse.EnsureSuccessStatusCode();

        var inbox = await GetRequiredJsonAsync<List<TaskShareInboxResponse>>(
            sharedClient,
            "/api/account/task-shares");

        Assert.NotNull(inbox);
        var share = Assert.Single(inbox!);
        Assert.Equal(sharedTask.Id, share.TaskItemId);
        Assert.Equal(ownerWorkspaceId, share.WorkspaceId);
        Assert.Equal(owner.User.Email, share.SharedByEmail);

        var leaveResponse = await sharedClient.DeleteAsync(
            $"/api/account/task-shares/{share.ShareId}");
        leaveResponse.EnsureSuccessStatusCode();

        var remainingShares = await GetRequiredJsonAsync<List<TaskShareInboxResponse>>(
            sharedClient,
            "/api/account/task-shares");
        var workspaces = await GetRequiredJsonAsync<List<WorkspaceResponse>>(
            sharedClient,
            "/api/workspaces");

        Assert.Empty(remainingShares!);
        Assert.DoesNotContain(workspaces!, workspace => workspace.Id == ownerWorkspaceId);
    }

    [Fact]
    public async Task TaskShare_AcceptedWorkspaceSharesCanBeLeftTogether()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var sharedClient = factory.CreateClient();
        var owner = await RegisterAndLoginAsync(
            ownerClient,
            "leave-workspace-shares-owner@example.com",
            "correct horse battery");
        var sharedUser = await RegisterAndLoginAsync(
            sharedClient,
            "leave-workspace-shares-user@example.com",
            "correct horse battery");
        var ownerWorkspaceId = owner.Workspaces.Single().Id;
        var firstTask = await CreateTaskAsync(ownerClient, "First accepted shared task");
        var secondTask = await CreateTaskAsync(ownerClient, "Second accepted shared task");

        var shareResponse = await ownerClient.PostAsJsonAsync(
            "/api/tasks/share-links",
            new
            {
                taskItemIds = new[] { firstTask.Id, secondTask.Id },
                email = sharedUser.User.Email
            });
        shareResponse.EnsureSuccessStatusCode();
        var link = (await shareResponse.Content.ReadFromJsonAsync<TaskShareLinkResponse>())!;
        var acceptResponse = await sharedClient.PostAsJsonAsync(
            "/api/share-links/accept",
            new
            {
                token = link.Token
            });
        acceptResponse.EnsureSuccessStatusCode();

        SetWorkspaceHeader(sharedClient, ownerWorkspaceId);
        var visibleBeforeLeaveResponse = await sharedClient.GetAsync(
            "/api/tasks");
        var visibleBeforeLeaveBody = await visibleBeforeLeaveResponse.Content.ReadAsStringAsync();
        Assert.True(
            visibleBeforeLeaveResponse.IsSuccessStatusCode,
            visibleBeforeLeaveBody);
        var visibleBeforeLeave = await visibleBeforeLeaveResponse.Content
            .ReadFromJsonAsync<List<TaskItemSummaryResponse>>();

        var leaveResponse = await sharedClient.DeleteAsync(
            $"/api/account/workspaces/{ownerWorkspaceId}/task-shares");
        leaveResponse.EnsureSuccessStatusCode();

        var workspaces = await GetRequiredJsonAsync<List<WorkspaceResponse>>(
            sharedClient,
            "/api/workspaces");
        SetWorkspaceHeader(sharedClient, ownerWorkspaceId);
        var firstDetailAfterLeave = await sharedClient.GetAsync($"/api/tasks/{firstTask.Id}");

        Assert.Contains(visibleBeforeLeave!, taskItem => taskItem.Id == firstTask.Id);
        Assert.Contains(visibleBeforeLeave!, taskItem => taskItem.Id == secondTask.Id);
        Assert.DoesNotContain(workspaces!, workspace => workspace.Id == ownerWorkspaceId);
        Assert.Equal(HttpStatusCode.NotFound, firstDetailAfterLeave.StatusCode);
    }

    [Fact]
    public async Task WorkspaceMember_OwnerCanRemoveMember()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await RegisterAndLoginAsync(
            ownerClient,
            "remove-member-owner@example.com",
            "correct horse battery");
        var member = await RegisterAndLoginAsync(
            memberClient,
            "remove-member-user@example.com",
            "correct horse battery");
        var ownerWorkspaceId = owner.Workspaces.Single().Id;

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            "/api/workspace/invitations",
            new
            {
                email = member.User.Email
            });
        inviteResponse.EnsureSuccessStatusCode();
        var invite = await inviteResponse.Content.ReadFromJsonAsync<WorkspaceInvitationResponse>();
        var acceptResponse = await memberClient.PostAsJsonAsync(
            "/api/workspace/invitations/accept",
            new
            {
                token = invite!.Token
            });
        acceptResponse.EnsureSuccessStatusCode();

        var removeResponse = await ownerClient.DeleteAsync(
            $"/api/workspace/members/{member.User.Id}");
        removeResponse.EnsureSuccessStatusCode();

        var members = await ownerClient.GetFromJsonAsync<List<WorkspaceMemberResponse>>(
            "/api/workspace/members");
        var workspaces = await GetRequiredJsonAsync<List<WorkspaceResponse>>(
            memberClient,
            "/api/workspaces");

        Assert.DoesNotContain(members!, workspaceMember => workspaceMember.UserId == member.User.Id);
        Assert.DoesNotContain(workspaces!, workspace => workspace.Id == ownerWorkspaceId);
    }

    [Fact]
    public async Task TaskShare_FilterBySharedWithReturnsSharedTasks()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var sharedClient = factory.CreateClient();
        await RegisterAndLoginAsync(
            ownerClient,
            "filter-owner@example.com",
            "correct horse battery");
        var sharedUser = await RegisterAndLoginAsync(
            sharedClient,
            "filter-shared@example.com",
            "correct horse battery");
        var sharedTask = await CreateTaskAsync(ownerClient, "Shared by filter");
        var privateTask = await CreateTaskAsync(ownerClient, "Not shared by filter");

        var shareResponse = await ownerClient.PostAsJsonAsync(
            $"/api/tasks/{sharedTask.Id}/shares",
            new
            {
                email = sharedUser.User.Email
            });
        shareResponse.EnsureSuccessStatusCode();

        var filteredTasks = await ownerClient.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            $"/api/tasks?sharedWith={Uri.EscapeDataString(sharedUser.User.Email)}");

        Assert.Contains(filteredTasks!, taskItem => taskItem.Id == sharedTask.Id);
        Assert.DoesNotContain(filteredTasks!, taskItem => taskItem.Id == privateTask.Id);
    }

    [Fact]
    public async Task TaskShare_SharedOnlyUserCannotManageShares()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var sharedClient = factory.CreateClient();
        var owner = await RegisterAndLoginAsync(
            ownerClient,
            "manage-owner@example.com",
            "correct horse battery");
        var sharedUser = await RegisterAndLoginAsync(
            sharedClient,
            "manage-shared@example.com",
            "correct horse battery");
        var ownerWorkspaceId = owner.Workspaces.Single().Id;
        var sharedTask = await CreateTaskAsync(ownerClient, "Shared management task");

        var shareResponse = await ownerClient.PostAsJsonAsync(
            $"/api/tasks/{sharedTask.Id}/shares",
            new
            {
                email = sharedUser.User.Email
            });
        shareResponse.EnsureSuccessStatusCode();

        SetWorkspaceHeader(sharedClient, ownerWorkspaceId);
        var reshareResponse = await sharedClient.PostAsJsonAsync(
            $"/api/tasks/{sharedTask.Id}/shares",
            new
            {
                email = "third@example.com"
            });
        var updateResponse = await sharedClient.PatchAsJsonAsync(
            $"/api/tasks/{sharedTask.Id}",
            new
            {
                title = "Shared user can edit the task"
            });

        Assert.Equal(HttpStatusCode.NotFound, reshareResponse.StatusCode);
        updateResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task TaskShare_ViewerCanReadButCannotEditSharedTask()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        using var viewerClient = factory.CreateClient();
        var owner = await RegisterAndLoginAsync(
            ownerClient,
            "viewer-share-owner@example.com",
            "correct horse battery");
        var viewer = await RegisterAndLoginAsync(
            viewerClient,
            "viewer-share-user@example.com",
            "correct horse battery");
        var ownerWorkspaceId = owner.Workspaces.Single().Id;
        var sharedTask = await CreateTaskAsync(ownerClient, "Viewer task share");

        var shareResponse = await ownerClient.PostAsJsonAsync(
            $"/api/tasks/{sharedTask.Id}/shares",
            new
            {
                email = viewer.User.Email,
                role = "Viewer"
            });
        shareResponse.EnsureSuccessStatusCode();

        SetWorkspaceHeader(viewerClient, ownerWorkspaceId);
        var detailResponse = await viewerClient.GetAsync($"/api/tasks/{sharedTask.Id}");
        var updateResponse = await viewerClient.PatchAsJsonAsync(
            $"/api/tasks/{sharedTask.Id}",
            new
            {
                title = "Viewer should not edit"
            });

        detailResponse.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
    }

    [Fact]
    public async Task TaskCopy_CopiesSelectedTasksToAnotherBoardWithCleanTimeline()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        await RegisterAndLoginAsync(
            ownerClient,
            "copy-board-owner@example.com",
            "correct horse battery");
        var destinationWorkspace = await CreateWorkspaceAsync(ownerClient, "Copied tasks board");
        var sourceTask = await CreateTaskAsync(ownerClient, "Copy source task");
        var followUpAt = DateTimeOffset.UtcNow.AddDays(3);

        var updateResponse = await ownerClient.PatchAsJsonAsync(
            $"/api/tasks/{sourceTask.Id}",
            new
            {
                status = "Waiting",
                category = "People",
                color = "#FDE68A",
                followUpAt
            });
        updateResponse.EnsureSuccessStatusCode();
        var noteResponse = await ownerClient.PostAsJsonAsync(
            $"/api/tasks/{sourceTask.Id}/timeline",
            new
            {
                note = "Original note should not be copied by default."
            });
        noteResponse.EnsureSuccessStatusCode();

        var copyResponse = await ownerClient.PostAsJsonAsync(
            "/api/tasks/copy",
            new
            {
                taskItemIds = new[] { sourceTask.Id },
                destinationWorkspaceId = destinationWorkspace.Id
            });
        copyResponse.EnsureSuccessStatusCode();
        var copy = await copyResponse.Content.ReadFromJsonAsync<CopyTaskItemsResponse>();
        var copiedTask = Assert.Single(copy!.Tasks);

        Assert.Equal(destinationWorkspace.Id, copiedTask.WorkspaceId);
        Assert.Equal("Copy source task", copiedTask.Title);
        Assert.Equal("Waiting", copiedTask.Status);
        Assert.Equal("People", copiedTask.Category);
        Assert.Equal("#FDE68A", copiedTask.Color);
        Assert.Equal(followUpAt, copiedTask.FollowUpAt);
        Assert.DoesNotContain(
            copiedTask.TimelineEntries,
            entry => entry.Details == "Original note should not be copied by default.");
        Assert.Contains(
            copiedTask.TimelineEntries,
            entry => entry.Kind == "NoteAdded" && entry.Details!.Contains("Copied from"));

        SetWorkspaceHeader(ownerClient, destinationWorkspace.Id);
        var destinationTasks = await ownerClient.GetFromJsonAsync<List<TaskItemSummaryResponse>>(
            "/api/tasks");

        Assert.Contains(destinationTasks!, taskItem => taskItem.Id == copiedTask.Id);
    }

    [Fact]
    public async Task TaskCopy_PreservesTemplateAndFieldValuesWithinSameBoard()
    {
        using var factory = new DumpTetherApiFactory();
        using var ownerClient = factory.CreateClient();
        await RegisterAndLoginAsync(
            ownerClient,
            "copy-template-owner@example.com",
            "correct horse battery");
        var template = await CreateTemplateAsync(
            ownerClient,
            "Copy template",
            [
                new
                {
                    name = "Customer",
                    type = "Text",
                    required = false,
                    sortOrder = 1,
                    options = Array.Empty<string>()
                }
            ]);
        var customerField = Assert.Single(template.Fields);
        var sourceTaskResponse = await ownerClient.PostAsJsonAsync(
            "/api/tasks",
            new
            {
                title = "Template copy source",
                taskTemplateId = template.Id,
                fieldValues = new Dictionary<Guid, object?>
                {
                    [customerField.Id] = "Northwind"
                }
            });
        sourceTaskResponse.EnsureSuccessStatusCode();
        var sourceTask = (await sourceTaskResponse.Content.ReadFromJsonAsync<TaskItemDetailResponse>())!;

        var copyResponse = await ownerClient.PostAsJsonAsync(
            "/api/tasks/copy",
            new
            {
                taskItemIds = new[] { sourceTask.Id },
                destinationWorkspaceId = sourceTask.WorkspaceId
            });
        copyResponse.EnsureSuccessStatusCode();
        var copy = await copyResponse.Content.ReadFromJsonAsync<CopyTaskItemsResponse>();
        var copiedTask = Assert.Single(copy!.Tasks);
        var copiedField = Assert.Single(copiedTask.FieldValues);

        Assert.Equal(template.Id, copiedTask.TaskTemplateId);
        Assert.Equal(customerField.Id, copiedField.FieldDefinitionId);
        Assert.Contains("Northwind", copiedField.ValueJson);
    }

    private static async Task<LoginUserResponse> RegisterAndLoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email,
                password,
                displayName = email.Split('@')[0]
            });
        var registerBody = await registerResponse.Content.ReadAsStringAsync();
        Assert.True(
            registerResponse.StatusCode == HttpStatusCode.Created,
            $"Expected Created, got {registerResponse.StatusCode}. Body: {registerBody}");

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email,
                password,
                deviceName = "test client"
            });
        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        Assert.True(
            loginResponse.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {loginResponse.StatusCode}. Body: {loginBody}");

        var login = (await loginResponse.Content.ReadFromJsonAsync<LoginUserResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.SessionToken);

        return login;
    }

    private static async Task<TaskItemDetailResponse> CreateTaskAsync(
        HttpClient client,
        string title)
    {
        var response = await client.PostAsJsonAsync(
            "/api/tasks",
            new
            {
                title
            });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created, got {response.StatusCode}. Body: {body}");

        return (await response.Content.ReadFromJsonAsync<TaskItemDetailResponse>())!;
    }

    private static async Task<WorkspaceResponse> CreateWorkspaceAsync(
        HttpClient client,
        string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/workspaces",
            new
            {
                name
            });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created, got {response.StatusCode}. Body: {body}");

        return (await response.Content.ReadFromJsonAsync<WorkspaceResponse>())!;
    }

    private static async Task<TaskTemplateDetailResponse> CreateTemplateAsync(
        HttpClient client,
        string name,
        object[] fields)
    {
        var response = await client.PostAsJsonAsync(
            "/api/templates",
            new
            {
                name,
                fields
            });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created, got {response.StatusCode}. Body: {body}");

        return (await response.Content.ReadFromJsonAsync<TaskTemplateDetailResponse>())!;
    }

    private static async Task<T> GetRequiredJsonAsync<T>(
        HttpClient client,
        string requestUri)
    {
        var response = await client.GetAsync(requestUri);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected success for GET {requestUri}, got {response.StatusCode}. Body: {body}");

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static void SetWorkspaceHeader(HttpClient client, Guid workspaceId)
    {
        client.DefaultRequestHeaders.Remove("X-DumpTether-Workspace-Id");
        client.DefaultRequestHeaders.Add("X-DumpTether-Workspace-Id", workspaceId.ToString());
    }
}
