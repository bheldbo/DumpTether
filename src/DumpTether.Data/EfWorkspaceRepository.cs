using DumpTether.App.Workspaces;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;

namespace DumpTether.Data;

internal sealed class EfWorkspaceRepository : IWorkspaceRepository
{
    private readonly DumpTetherDbContext _dbContext;

    public EfWorkspaceRepository(DumpTetherDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Workspace>> ListAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Workspaces
            .AsNoTracking()
            .OrderBy(workspace => workspace.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Workspace>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return [];
        }

        var memberWorkspaces = await _dbContext.WorkspaceMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .Join(
                _dbContext.Workspaces.AsNoTracking(),
                membership => membership.WorkspaceId,
                workspace => workspace.Id,
                (_, workspace) => workspace)
            .ToListAsync(cancellationToken);

        var sharedWorkspaces = await _dbContext.TaskItemShares
            .AsNoTracking()
            .Where(share =>
                share.RevokedAt == null &&
                (share.AcceptedAt != null || share.TokenHash == null) &&
                (share.SharedWithUserId == userId ||
                    share.NormalizedEmail == user.NormalizedEmail))
            .Join(
                _dbContext.Workspaces.AsNoTracking(),
                share => share.WorkspaceId,
                workspace => workspace.Id,
                (_, workspace) => workspace)
            .ToListAsync(cancellationToken);

        return memberWorkspaces
            .Concat(sharedWorkspaces)
            .GroupBy(workspace => workspace.Id)
            .Select(group => group.First())
            .OrderBy(workspace => workspace.Name)
            .ToList();
    }

    public async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Workspaces
            .SingleOrDefaultAsync(workspace => workspace.Id == id, cancellationToken);
    }

    public async Task<WorkspaceMembership?> GetMembershipAsync(
        Guid workspaceId,
        Guid userId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.WorkspaceMemberships
            .Where(membership =>
                membership.WorkspaceId == workspaceId &&
                membership.UserId == userId);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkspaceMember>> ListMembersAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var members = await _dbContext.WorkspaceMemberships
            .AsNoTracking()
            .Where(membership => membership.WorkspaceId == workspaceId)
            .Join(
                _dbContext.AppUsers.AsNoTracking(),
                membership => membership.UserId,
                user => user.Id,
                (membership, user) => new { membership, user })
            .ToListAsync(cancellationToken);

        return members
            .OrderBy(item => item.membership.Role)
            .ThenBy(item => item.user.DisplayName)
            .ThenBy(item => item.user.Email)
            .Select(item => new WorkspaceMember(item.user, item.membership))
            .ToList();
    }

    public async Task AddMembershipAsync(
        WorkspaceMembership membership,
        CancellationToken cancellationToken)
    {
        await _dbContext.WorkspaceMemberships.AddAsync(membership, cancellationToken);
    }

    public void RemoveMembership(WorkspaceMembership membership)
    {
        _dbContext.WorkspaceMemberships.Remove(membership);
    }

    public async Task<IReadOnlyList<WorkspaceInvitation>> ListInvitationsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var invitations = await _dbContext.WorkspaceInvitations
            .AsNoTracking()
            .Where(invitation => invitation.WorkspaceId == workspaceId)
            .ToListAsync(cancellationToken);

        return invitations
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<WorkspaceInvitationInboxItem>> ListIncomingInvitationsAsync(
        string normalizedEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invitationCandidates = await _dbContext.WorkspaceInvitations
            .AsNoTracking()
            .Where(invitation =>
                invitation.NormalizedEmail == normalizedEmail &&
                invitation.AcceptedAt == null &&
                invitation.RevokedAt == null)
            .ToListAsync(cancellationToken);
        var invitationIds = invitationCandidates
            .Where(invitation => invitation.IsUsable(now))
            .Select(invitation => invitation.Id)
            .ToArray();

        if (invitationIds.Length == 0)
        {
            return [];
        }

        var items = await _dbContext.WorkspaceInvitations
            .AsNoTracking()
            .Where(invitation => invitationIds.Contains(invitation.Id))
            .Join(
                _dbContext.Workspaces.AsNoTracking(),
                invitation => invitation.WorkspaceId,
                workspace => workspace.Id,
                (invitation, workspace) => new { invitation, workspace })
            .Join(
                _dbContext.AppUsers.AsNoTracking(),
                item => item.invitation.InvitedByUserId,
                user => user.Id,
                (item, user) => new WorkspaceInvitationInboxItem(
                    item.invitation,
                    item.workspace,
                    user))
            .ToListAsync(cancellationToken);

        return items
            .OrderByDescending(item => item.Invitation.CreatedAt)
            .ToList();
    }

    public async Task<WorkspaceInvitation?> GetInvitationByIdAsync(
        Guid workspaceId,
        Guid invitationId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.WorkspaceInvitations
            .Where(invitation =>
                invitation.WorkspaceId == workspaceId &&
                invitation.Id == invitationId);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<WorkspaceInvitation?> GetIncomingInvitationByIdAsync(
        Guid invitationId,
        string normalizedEmail,
        DateTimeOffset now,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.WorkspaceInvitations
            .Where(invitation =>
                invitation.Id == invitationId &&
                invitation.NormalizedEmail == normalizedEmail &&
                invitation.AcceptedAt == null &&
                invitation.RevokedAt == null);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        var invitation = await query.SingleOrDefaultAsync(cancellationToken);
        return invitation is not null && invitation.IsUsable(now)
            ? invitation
            : null;
    }

    public async Task<WorkspaceInvitation?> GetInvitationByTokenHashAsync(
        string tokenHash,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.WorkspaceInvitations
            .Where(invitation => invitation.TokenHash == tokenHash);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> HasUsableInvitationAsync(
        Guid workspaceId,
        string normalizedEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invitations = await _dbContext.WorkspaceInvitations
            .AsNoTracking()
            .Where(invitation =>
                invitation.WorkspaceId == workspaceId &&
                invitation.NormalizedEmail == normalizedEmail &&
                invitation.AcceptedAt == null &&
                invitation.RevokedAt == null)
            .ToListAsync(cancellationToken);

        return invitations.Any(invitation => invitation.IsUsable(now));
    }

    public async Task AddAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        await _dbContext.Workspaces.AddAsync(workspace, cancellationToken);
    }

    public async Task AddInvitationAsync(
        WorkspaceInvitation invitation,
        CancellationToken cancellationToken)
    {
        await _dbContext.WorkspaceInvitations.AddAsync(invitation, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var workspace = await _dbContext.Workspaces
            .SingleOrDefaultAsync(candidate => candidate.Id == workspaceId, cancellationToken);

        if (workspace is null)
        {
            return false;
        }

        var taskIds = await _dbContext.TaskItems
            .Where(taskItem => taskItem.WorkspaceId == workspaceId)
            .Select(taskItem => taskItem.Id)
            .ToListAsync(cancellationToken);

        if (taskIds.Count > 0)
        {
            var timelineEntryIds = await _dbContext.TaskTimelineEntries
                .Where(entry => taskIds.Contains(entry.TaskItemId))
                .Select(entry => entry.Id)
                .ToListAsync(cancellationToken);

            _dbContext.FieldValues.RemoveRange(
                await _dbContext.FieldValues
                    .Where(fieldValue => taskIds.Contains(fieldValue.TaskItemId))
                    .ToListAsync(cancellationToken));
            if (timelineEntryIds.Count > 0)
            {
                _dbContext.TaskTimelineEntryFieldValues.RemoveRange(
                    await _dbContext.TaskTimelineEntryFieldValues
                        .Where(fieldValue => timelineEntryIds.Contains(fieldValue.TaskTimelineEntryId))
                        .ToListAsync(cancellationToken));
            }

            _dbContext.TaskTimelineEntries.RemoveRange(
                await _dbContext.TaskTimelineEntries
                    .Where(entry => taskIds.Contains(entry.TaskItemId))
                    .ToListAsync(cancellationToken));
            _dbContext.TaskItemShares.RemoveRange(
                await _dbContext.TaskItemShares
                    .Where(share => taskIds.Contains(share.TaskItemId))
                    .ToListAsync(cancellationToken));
            _dbContext.TaskItems.RemoveRange(
                await _dbContext.TaskItems
                    .Where(taskItem => taskIds.Contains(taskItem.Id))
                    .ToListAsync(cancellationToken));
        }

        _dbContext.SavedViews.RemoveRange(
            await _dbContext.SavedViews
                .Where(view => view.WorkspaceId == workspaceId)
                .ToListAsync(cancellationToken));
        _dbContext.ArchiveResolutions.RemoveRange(
            await _dbContext.ArchiveResolutions
                .Where(resolution => resolution.WorkspaceId == workspaceId)
                .ToListAsync(cancellationToken));
        _dbContext.Projects.RemoveRange(
            await _dbContext.Projects
                .Where(project => project.WorkspaceId == workspaceId)
                .ToListAsync(cancellationToken));
        _dbContext.WorkspaceInvitations.RemoveRange(
            await _dbContext.WorkspaceInvitations
                .Where(invitation => invitation.WorkspaceId == workspaceId)
                .ToListAsync(cancellationToken));
        _dbContext.WorkspaceMemberships.RemoveRange(
            await _dbContext.WorkspaceMemberships
                .Where(membership => membership.WorkspaceId == workspaceId)
                .ToListAsync(cancellationToken));
        _dbContext.Workspaces.Remove(workspace);

        return true;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
