using DumpTether.App.Auth;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;

namespace DumpTether.Data;

internal sealed class EfAuthRepository : IAuthRepository
{
    private readonly DumpTetherDbContext _dbContext;

    public EfAuthRepository(DumpTetherDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AppUser?> GetUserByNormalizedEmailAsync(
        string normalizedEmail,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.AppUsers
            .Where(user => user.NormalizedEmail == normalizedEmail);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AppUser?> GetUserByIdAsync(
        Guid id,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.AppUsers
            .Where(user => user.Id == id);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<UserSession?> GetSessionByTokenHashAsync(
        string sessionTokenHash,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.UserSessions
            .Where(session => session.SessionTokenHash == sessionTokenHash);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserWorkspaceMembership>> ListWorkspacesForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var memberships = await _dbContext.WorkspaceMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .Join(
                _dbContext.Workspaces.AsNoTracking(),
                membership => membership.WorkspaceId,
                workspace => workspace.Id,
                (membership, workspace) => new { membership, workspace })
            .ToListAsync(cancellationToken);

        return memberships
            .OrderBy(item => item.membership.CreatedAt)
            .ThenBy(item => item.workspace.Name)
            .Select(item => new UserWorkspaceMembership(item.workspace, item.membership))
            .ToList();
    }

    public async Task AddUserAsync(AppUser user, CancellationToken cancellationToken)
    {
        await _dbContext.AppUsers.AddAsync(user, cancellationToken);
    }

    public async Task AddSessionAsync(UserSession session, CancellationToken cancellationToken)
    {
        await _dbContext.UserSessions.AddAsync(session, cancellationToken);
    }

    public async Task AddWorkspaceMembershipAsync(
        WorkspaceMembership membership,
        CancellationToken cancellationToken)
    {
        await _dbContext.WorkspaceMemberships.AddAsync(membership, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
