using System.ComponentModel.DataAnnotations;
using DumpTether.App.Auth;
using DumpTether.App.LiveUpdates;
using DumpTether.App.Tasks;
using DumpTether.Domain;
using Microsoft.Extensions.Options;

namespace DumpTether.App.Workspaces;

internal sealed class WorkspaceService : IWorkspaceService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(1);

    private readonly IAuthRepository _authRepository;
    private readonly IClock _clock;
    private readonly IOptions<AuthOptions> _authOptions;
    private readonly ICurrentUserSessionProvider _currentUserSessionProvider;
    private readonly IDevelopmentWorkspaceProvider _developmentWorkspaceProvider;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;
    private readonly ISessionTokenService _sessionTokenService;
    private readonly IWorkspaceRepository _workspaceRepository;

    public WorkspaceService(
        IAuthRepository authRepository,
        IClock clock,
        IOptions<AuthOptions> authOptions,
        ICurrentUserSessionProvider currentUserSessionProvider,
        IDevelopmentWorkspaceProvider developmentWorkspaceProvider,
        ILiveUpdatePublisher liveUpdatePublisher,
        ISessionTokenService sessionTokenService,
        IWorkspaceRepository workspaceRepository)
    {
        _authRepository = authRepository;
        _clock = clock;
        _authOptions = authOptions;
        _currentUserSessionProvider = currentUserSessionProvider;
        _developmentWorkspaceProvider = developmentWorkspaceProvider;
        _liveUpdatePublisher = liveUpdatePublisher;
        _sessionTokenService = sessionTokenService;
        _workspaceRepository = workspaceRepository;
    }

    public async Task<IReadOnlyList<WorkspaceResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);

        if (currentSession is null)
        {
            var workspaces = await _workspaceRepository.ListAsync(cancellationToken);

            return SortWorkspaceResponses(workspaces.Select(MapWorkspace));
        }

        var workspaceMemberships = await _authRepository.ListWorkspacesForUserAsync(
            currentSession.UserId,
            cancellationToken);
        workspaceMemberships = await EnsureStandardWorkspaceAsync(
            currentSession.UserId,
            workspaceMemberships,
            cancellationToken);

        var responses = new List<WorkspaceResponse>();

        foreach (var membership in workspaceMemberships)
        {
            responses.Add(await MapWorkspaceAsync(membership, cancellationToken));
        }

        return SortWorkspaceResponses(responses);
    }

    public async Task<WorkspaceResponse> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var workspace = await GetCurrentWorkspaceAsync(cancellationToken);
        return MapWorkspace(workspace);
    }

    public async Task<WorkspaceResponse> CreateAsync(
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);

        if (currentSession is null && _authOptions.Value.RequireAuthentication)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        await EnsureWorkspaceNameIsAvailableAsync(
            request.Name,
            exceptWorkspaceId: null,
            currentSession,
            cancellationToken);

        var workspace = Workspace.Create(request.Name, _clock.UtcNow);

        if (request.Color is not null)
        {
            workspace.ChangeColor(request.Color, _clock.UtcNow);
        }

        if (currentSession is not null)
        {
            workspace.AddMembership(
                currentSession.UserId,
                WorkspaceMembershipRole.Owner,
                _clock.UtcNow);
        }

        await _workspaceRepository.AddAsync(workspace, cancellationToken);
        await _workspaceRepository.SaveChangesAsync(cancellationToken);
        await PublishWorkspaceEventAsync(
            LiveUpdateEvents.WorkspaceCreated,
            workspace.Id,
            currentSession?.UserId,
            currentSession is null ? null : [currentSession.UserId],
            cancellationToken);

        return MapWorkspace(workspace);
    }

    public async Task<WorkspaceResponse> UpdateCurrentAsync(
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentSession = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);
        var workspace = currentSession is null
            ? await GetCurrentWorkspaceAsync(cancellationToken)
            : (await RequireCurrentMembershipAsync(
                requireOwner: true,
                trackChanges: true,
                cancellationToken)).Workspace;

        if (request.Name is not null)
        {
            EnsureSystemWorkspaceCanBeRenamed(workspace, request.Name);
            await EnsureWorkspaceNameIsAvailableAsync(
                request.Name,
                workspace.Id,
                currentSession,
                cancellationToken);
            workspace.Rename(request.Name, _clock.UtcNow);
        }

        if (request.Color is not null)
        {
            workspace.ChangeColor(request.Color, _clock.UtcNow);
        }

        await _workspaceRepository.SaveChangesAsync(cancellationToken);
        await PublishWorkspaceEventForMembersAsync(
            LiveUpdateEvents.WorkspaceUpdated,
            workspace.Id,
            currentSession?.UserId,
            cancellationToken);

        return MapWorkspace(workspace);
    }

    public async Task<WorkspaceResponse?> UpdateAsync(
        Guid workspaceId,
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (workspace, currentSession, _) = await RequireWorkspaceMembershipAsync(
            workspaceId,
            requireOwner: true,
            trackChanges: true,
            cancellationToken);

        if (request.Name is not null)
        {
            EnsureSystemWorkspaceCanBeRenamed(workspace, request.Name);
            await EnsureWorkspaceNameIsAvailableAsync(
                request.Name,
                workspace.Id,
                currentSession,
                cancellationToken);
            workspace.Rename(request.Name, _clock.UtcNow);
        }

        if (request.Color is not null)
        {
            workspace.ChangeColor(request.Color, _clock.UtcNow);
        }

        await _workspaceRepository.SaveChangesAsync(cancellationToken);
        await PublishWorkspaceEventForMembersAsync(
            LiveUpdateEvents.WorkspaceUpdated,
            workspace.Id,
            currentSession.UserId,
            cancellationToken);

        return MapWorkspace(workspace);
    }

    public async Task<IReadOnlyList<WorkspaceMemberResponse>> ListMembersAsync(
        CancellationToken cancellationToken)
    {
        var (workspace, _, _) = await RequireCurrentMembershipAsync(
            requireOwner: false,
            trackChanges: false,
            cancellationToken);
        var members = await _workspaceRepository.ListMembersAsync(
            workspace.Id,
            cancellationToken);

        return members.Select(MapMember).ToList();
    }

    public async Task<IReadOnlyList<WorkspaceInvitationResponse>> ListInvitationsAsync(
        CancellationToken cancellationToken)
    {
        var (workspace, _, _) = await RequireCurrentMembershipAsync(
            requireOwner: true,
            trackChanges: false,
            cancellationToken);
        var invitations = await _workspaceRepository.ListInvitationsAsync(
            workspace.Id,
            cancellationToken);

        return invitations.Select(invitation => MapInvitation(invitation, token: null)).ToList();
    }

    public async Task<IReadOnlyList<WorkspaceInvitationInboxResponse>> ListIncomingInvitationsAsync(
        CancellationToken cancellationToken)
    {
        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        var invitations = await _workspaceRepository.ListIncomingInvitationsAsync(
            AppUser.NormalizeEmail(currentSession.Email),
            _clock.UtcNow,
            cancellationToken);

        return invitations.Select(MapIncomingInvitation).ToList();
    }

    public async Task<WorkspaceInvitationResponse> CreateInvitationAsync(
        CreateWorkspaceInvitationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (workspace, currentSession, _) = await RequireCurrentMembershipAsync(
            requireOwner: true,
            trackChanges: false,
            cancellationToken);
        var normalizedEmail = AppUser.NormalizeEmail(request.Email);
        var existingUser = await _authRepository.GetUserByNormalizedEmailAsync(
            normalizedEmail,
            trackChanges: false,
            cancellationToken);

        if (existingUser is not null)
        {
            var existingMembership = await _workspaceRepository.GetMembershipAsync(
                workspace.Id,
                existingUser.Id,
                trackChanges: false,
                cancellationToken);

            if (existingMembership is not null)
            {
                throw new ValidationException("User is already a member of this board.");
            }
        }

        var now = _clock.UtcNow;
        if (await _workspaceRepository.HasUsableInvitationAsync(
                workspace.Id,
                normalizedEmail,
                now,
                cancellationToken))
        {
            throw new ValidationException("A pending invitation already exists for this email.");
        }

        var token = _sessionTokenService.CreateSessionToken();
        var invitation = WorkspaceInvitation.Create(
            workspace.Id,
            request.Email,
            request.Role,
            _sessionTokenService.HashToken(token),
            currentSession.UserId,
            now,
            now.Add(InvitationLifetime));

        await _workspaceRepository.AddInvitationAsync(invitation, cancellationToken);
        await _workspaceRepository.SaveChangesAsync(cancellationToken);

        return MapInvitation(invitation, token);
    }

    public async Task<WorkspaceInvitationResponse> AcceptInvitationAsync(
        AcceptWorkspaceInvitationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        WorkspaceInvitation? invitation;

        if (request.InvitationId.HasValue && request.InvitationId.Value != Guid.Empty)
        {
            invitation = await _workspaceRepository.GetIncomingInvitationByIdAsync(
                request.InvitationId.Value,
                AppUser.NormalizeEmail(currentSession.Email),
                _clock.UtcNow,
                trackChanges: true,
                cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.Token))
        {
            invitation = await _workspaceRepository.GetInvitationByTokenHashAsync(
                _sessionTokenService.HashToken(request.Token),
                trackChanges: true,
                cancellationToken);
        }
        else
        {
            throw new ValidationException("Invitation token or id is required.");
        }

        return await AcceptInvitationCoreAsync(
            invitation,
            currentSession,
            cancellationToken);
    }

    public async Task<WorkspaceInvitationResponse> AcceptInvitationTokenAsync(
        string token,
        CancellationToken cancellationToken)
    {
        return await AcceptInvitationAsync(
            new AcceptWorkspaceInvitationRequest(token),
            cancellationToken);
    }

    public async Task<WorkspaceInvitationResponse> AcceptIncomingInvitationAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        var invitation = await _workspaceRepository.GetIncomingInvitationByIdAsync(
            invitationId,
            AppUser.NormalizeEmail(currentSession.Email),
            _clock.UtcNow,
            trackChanges: true,
            cancellationToken);

        return await AcceptInvitationCoreAsync(
            invitation,
            currentSession,
            cancellationToken);
    }

    public async Task<bool> DeclineIncomingInvitationAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        var invitation = await _workspaceRepository.GetIncomingInvitationByIdAsync(
            invitationId,
            AppUser.NormalizeEmail(currentSession.Email),
            _clock.UtcNow,
            trackChanges: true,
            cancellationToken);

        if (invitation is null)
        {
            return false;
        }

        invitation.Revoke(_clock.UtcNow);
        await _workspaceRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> RevokeInvitationAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var (workspace, _, _) = await RequireCurrentMembershipAsync(
            requireOwner: true,
            trackChanges: false,
            cancellationToken);
        var invitation = await _workspaceRepository.GetInvitationByIdAsync(
            workspace.Id,
            invitationId,
            trackChanges: true,
            cancellationToken);

        if (invitation is null)
        {
            return false;
        }

        invitation.Revoke(_clock.UtcNow);
        await _workspaceRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> RemoveMemberAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        var (workspace, currentSession, _) = await RequireCurrentMembershipAsync(
            requireOwner: true,
            trackChanges: false,
            cancellationToken);

        if (userId == currentSession.UserId)
        {
            throw new ValidationException("Use leave board to remove your own membership.");
        }

        var targetMembership = await _workspaceRepository.GetMembershipAsync(
            workspace.Id,
            userId,
            trackChanges: true,
            cancellationToken);

        if (targetMembership is null)
        {
            return false;
        }

        if (targetMembership.Role == WorkspaceMembershipRole.Owner)
        {
            throw new ValidationException("Board owners cannot be removed.");
        }

        _workspaceRepository.RemoveMembership(targetMembership);
        await _workspaceRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<WorkspaceMemberResponse?> UpdateMemberRoleAsync(
        Guid userId,
        UpdateWorkspaceMemberRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        var (workspace, currentSession, _) = await RequireCurrentMembershipAsync(
            requireOwner: true,
            trackChanges: false,
            cancellationToken);

        if (userId == currentSession.UserId)
        {
            throw new ValidationException("Use board settings to change your own role.");
        }

        if (request.Role == WorkspaceMembershipRole.Owner)
        {
            throw new ValidationException("Board ownership cannot be assigned here.");
        }

        var targetMembership = await _workspaceRepository.GetMembershipAsync(
            workspace.Id,
            userId,
            trackChanges: true,
            cancellationToken);

        if (targetMembership is null)
        {
            return null;
        }

        try
        {
            targetMembership.ChangeRole(request.Role);
        }
        catch (InvalidOperationException exception)
        {
            throw new ValidationException(exception.Message, exception);
        }

        await _workspaceRepository.SaveChangesAsync(cancellationToken);

        var members = await _workspaceRepository.ListMembersAsync(
            workspace.Id,
            cancellationToken);

        return members
            .Select(MapMember)
            .SingleOrDefault(member => member.UserId == userId);
    }

    public async Task<bool> LeaveCurrentWorkspaceAsync(CancellationToken cancellationToken)
    {
        var (workspace, _, membership) = await RequireCurrentMembershipAsync(
            requireOwner: false,
            trackChanges: true,
            cancellationToken);

        if (membership.Role == WorkspaceMembershipRole.Owner)
        {
            throw new ValidationException("Board owners cannot leave their own board.");
        }

        _workspaceRepository.RemoveMembership(membership);
        await _workspaceRepository.SaveChangesAsync(cancellationToken);

        return workspace.Id != Guid.Empty;
    }

    public async Task<bool> DeleteAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var (workspace, currentSession, _) = await RequireWorkspaceMembershipAsync(
            workspaceId,
            requireOwner: true,
            trackChanges: false,
            cancellationToken);

        if (IsSystemAllTasksWorkspace(workspace))
        {
            throw new ValidationException("All tasks is a standard board and cannot be deleted.");
        }

        var recipients = (await _workspaceRepository.ListMembersAsync(
                workspaceId,
                cancellationToken))
            .Select(member => member.User.Id)
            .ToArray();
        var deleted = await _workspaceRepository.DeleteAsync(workspaceId, cancellationToken);

        if (!deleted)
        {
            return false;
        }

        await _workspaceRepository.SaveChangesAsync(cancellationToken);
        await PublishWorkspaceEventAsync(
            LiveUpdateEvents.WorkspaceDeleted,
            workspaceId,
            currentSession.UserId,
            recipients,
            cancellationToken);

        return true;
    }

    private async Task PublishWorkspaceEventForMembersAsync(
        string eventName,
        Guid workspaceId,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        var recipients = (await _workspaceRepository.ListMembersAsync(
                workspaceId,
                cancellationToken))
            .Select(member => member.User.Id)
            .ToArray();

        await PublishWorkspaceEventAsync(
            eventName,
            workspaceId,
            actorUserId,
            recipients,
            cancellationToken);
    }

    private Task PublishWorkspaceEventAsync(
        string eventName,
        Guid workspaceId,
        Guid? actorUserId,
        IReadOnlyList<Guid>? recipientUserIds,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        return _liveUpdatePublisher.PublishAsync(
            new LiveUpdateMessage(
                eventName,
                workspaceId,
                null,
                null,
                actorUserId,
                now,
                now,
                recipientUserIds),
            cancellationToken);
    }

    private static bool IsSystemAllTasksWorkspace(Workspace workspace)
    {
        return string.Equals(
            workspace.Name.Trim(),
            "All Tasks",
            StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<WorkspaceResponse> SortWorkspaceResponses(
        IEnumerable<WorkspaceResponse> workspaces)
    {
        return workspaces
            .OrderBy(workspace => IsSystemAllTasksWorkspace(workspace) ? 0 : 1)
            .ThenBy(workspace => workspace.AccessKind == WorkspaceAccessKinds.TaskShare ? 1 : 0)
            .ThenBy(workspace => workspace.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsSystemAllTasksWorkspace(WorkspaceResponse workspace)
    {
        return workspace.AccessKind == WorkspaceAccessKinds.Membership &&
            string.Equals(
                workspace.Name.Trim(),
                "All Tasks",
                StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<UserWorkspaceMembership>> EnsureStandardWorkspaceAsync(
        Guid userId,
        IReadOnlyList<UserWorkspaceMembership> memberships,
        CancellationToken cancellationToken)
    {
        var hasStandardWorkspace = memberships.Any(membership =>
            membership.AccessKind == WorkspaceAccessKinds.Membership &&
            string.Equals(
                membership.Workspace.Name.Trim(),
                "All Tasks",
                StringComparison.OrdinalIgnoreCase));

        if (hasStandardWorkspace)
        {
            return memberships;
        }

        var now = _clock.UtcNow;
        var workspace = Workspace.Create("All Tasks", now);
        var membership = WorkspaceMembership.Create(
            workspace.Id,
            userId,
            WorkspaceMembershipRole.Owner,
            now);

        await _workspaceRepository.AddAsync(workspace, cancellationToken);
        await _authRepository.AddWorkspaceMembershipAsync(membership, cancellationToken);
        await _workspaceRepository.SaveChangesAsync(cancellationToken);

        return await _authRepository.ListWorkspacesForUserAsync(
            userId,
            cancellationToken);
    }

    private static void EnsureSystemWorkspaceCanBeRenamed(
        Workspace workspace,
        string requestedName)
    {
        if (!IsSystemAllTasksWorkspace(workspace))
        {
            return;
        }

        if (!string.Equals(
                requestedName.Trim(),
                "All Tasks",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("All tasks is a standard board and cannot be renamed.");
        }
    }

    private async Task<Workspace> GetCurrentWorkspaceAsync(CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var workspace = await _workspaceRepository.GetByIdAsync(
            context.WorkspaceId,
            cancellationToken);

        return workspace ?? throw new InvalidOperationException("Development workspace was not found.");
    }

    private async Task EnsureWorkspaceNameIsAvailableAsync(
        string name,
        Guid? exceptWorkspaceId,
        CurrentUserSession? currentSession,
        CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return;
        }

        var workspaces = currentSession is null
            ? await _workspaceRepository.ListAsync(cancellationToken)
            : (await _authRepository.ListWorkspacesForUserAsync(
                    currentSession.UserId,
                    cancellationToken))
                .Select(membership => membership.Workspace)
                .ToList();
        var nameIsTaken = workspaces.Any(workspace =>
            workspace.Id != exceptWorkspaceId &&
            string.Equals(workspace.Name.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase));

        if (nameIsTaken)
        {
            throw new ValidationException("A board with that name already exists.");
        }
    }

    private async Task<(Workspace Workspace, CurrentUserSession CurrentSession, WorkspaceMembership Membership)>
        RequireCurrentMembershipAsync(
            bool requireOwner,
            bool trackChanges,
            CancellationToken cancellationToken)
    {
        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        var workspace = await GetCurrentWorkspaceAsync(cancellationToken);
        var membership = await _workspaceRepository.GetMembershipAsync(
            workspace.Id,
            currentSession.UserId,
            trackChanges,
            cancellationToken);

        if (membership is null)
        {
            throw new UnauthorizedAccessException("Workspace membership is required.");
        }

        if (requireOwner && membership.Role != WorkspaceMembershipRole.Owner)
        {
            throw new UnauthorizedAccessException("Workspace owner role is required.");
        }

        return (workspace, currentSession, membership);
    }

    private async Task<(Workspace Workspace, CurrentUserSession CurrentSession, WorkspaceMembership Membership)>
        RequireWorkspaceMembershipAsync(
            Guid workspaceId,
            bool requireOwner,
            bool trackChanges,
            CancellationToken cancellationToken)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id is required.", nameof(workspaceId));
        }

        var currentSession = await RequireCurrentSessionAsync(cancellationToken);
        var workspace = await _workspaceRepository.GetByIdAsync(
            workspaceId,
            cancellationToken);

        if (workspace is null)
        {
            throw new ValidationException("Board was not found.");
        }

        var membership = await _workspaceRepository.GetMembershipAsync(
            workspace.Id,
            currentSession.UserId,
            trackChanges,
            cancellationToken);

        if (membership is null)
        {
            throw new UnauthorizedAccessException("Workspace membership is required.");
        }

        if (requireOwner && membership.Role != WorkspaceMembershipRole.Owner)
        {
            throw new UnauthorizedAccessException("Workspace owner role is required.");
        }

        return (workspace, currentSession, membership);
    }

    private async Task<CurrentUserSession> RequireCurrentSessionAsync(
        CancellationToken cancellationToken)
    {
        return await _currentUserSessionProvider.GetCurrentAsync(cancellationToken) ??
            throw new UnauthorizedAccessException("Authentication is required.");
    }

    private async Task<WorkspaceInvitationResponse> AcceptInvitationCoreAsync(
        WorkspaceInvitation? invitation,
        CurrentUserSession currentSession,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var normalizedEmail = AppUser.NormalizeEmail(currentSession.Email);

        if (invitation is null ||
            !invitation.IsUsable(now) ||
            !string.Equals(invitation.NormalizedEmail, normalizedEmail, StringComparison.Ordinal))
        {
            throw new ValidationException("Invitation is invalid or expired.");
        }

        var existingMembership = await _workspaceRepository.GetMembershipAsync(
            invitation.WorkspaceId,
            currentSession.UserId,
            trackChanges: false,
            cancellationToken);

        if (existingMembership is null)
        {
            await _workspaceRepository.AddMembershipAsync(
                WorkspaceMembership.Create(
                    invitation.WorkspaceId,
                    currentSession.UserId,
                    invitation.Role,
                    now),
                cancellationToken);
        }

        invitation.Accept(now);
        await _workspaceRepository.SaveChangesAsync(cancellationToken);
        await _liveUpdatePublisher.PublishAsync(
            new LiveUpdateMessage(
                LiveUpdateEvents.WorkspaceInviteAccepted,
                invitation.WorkspaceId,
                null,
                null,
                currentSession.UserId,
                now,
                now),
            cancellationToken);

        return MapInvitation(invitation, token: null);
    }

    private static WorkspaceResponse MapWorkspace(Workspace workspace)
    {
        return new WorkspaceResponse(
            workspace.Id,
            workspace.Name,
            workspace.Color,
            workspace.CreatedAt,
            UpdatedAt: workspace.UpdatedAt);
    }

    private static WorkspaceResponse MapWorkspace(UserWorkspaceMembership membership)
    {
        return new WorkspaceResponse(
            membership.Workspace.Id,
            membership.Workspace.Name,
            membership.Workspace.Color,
            membership.Workspace.CreatedAt,
            membership.AccessKind,
            membership.SharedTaskCount,
            UpdatedAt: membership.Workspace.UpdatedAt,
            Role: membership.Membership.Role);
    }

    private async Task<WorkspaceResponse> MapWorkspaceAsync(
        UserWorkspaceMembership membership,
        CancellationToken cancellationToken)
    {
        if (membership.AccessKind == WorkspaceAccessKinds.TaskShare)
        {
            return MapWorkspace(membership);
        }

        var members = await _workspaceRepository.ListMembersAsync(
            membership.Workspace.Id,
            cancellationToken);
        var invitations = await _workspaceRepository.ListInvitationsAsync(
            membership.Workspace.Id,
            cancellationToken);
        var now = _clock.UtcNow;

        return new WorkspaceResponse(
            membership.Workspace.Id,
            membership.Workspace.Name,
            membership.Workspace.Color,
            membership.Workspace.CreatedAt,
            membership.AccessKind,
            membership.SharedTaskCount,
            members.Count,
            invitations.Count(invitation =>
                invitation.AcceptedAt is null &&
                invitation.RevokedAt is null &&
                invitation.ExpiresAt > now),
            membership.Workspace.UpdatedAt,
            membership.Membership.Role);
    }

    private static WorkspaceMemberResponse MapMember(WorkspaceMember member)
    {
        return new WorkspaceMemberResponse(
            member.User.Id,
            member.User.Email,
            member.User.DisplayName,
            member.Membership.Role,
            member.Membership.CreatedAt);
    }

    private static WorkspaceInvitationResponse MapInvitation(
        WorkspaceInvitation invitation,
        string? token)
    {
        return new WorkspaceInvitationResponse(
            invitation.Id,
            invitation.WorkspaceId,
            invitation.Email,
            invitation.Role,
            invitation.CreatedAt,
            invitation.ExpiresAt,
            invitation.AcceptedAt,
            invitation.RevokedAt,
            token);
    }

    private static WorkspaceInvitationInboxResponse MapIncomingInvitation(
        WorkspaceInvitationInboxItem item)
    {
        return new WorkspaceInvitationInboxResponse(
            item.Invitation.Id,
            item.Workspace.Id,
            item.Workspace.Name,
            item.Workspace.Color,
            item.InvitedByUser.Email,
            item.InvitedByUser.DisplayName,
            item.Invitation.Role,
            item.Invitation.CreatedAt,
            item.Invitation.ExpiresAt);
    }
}
