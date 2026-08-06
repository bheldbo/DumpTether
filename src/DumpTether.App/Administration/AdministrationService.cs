using DumpTether.App.Auth;
using DumpTether.App.Tasks;
using DumpTether.Domain;

namespace DumpTether.App.Administration;

internal sealed class AdministrationService : IAdministrationService
{
    private const int MaximumListSize = 500;
    private readonly IAdministrationRepository _repository;
    private readonly IClock _clock;

    public AdministrationService(IAdministrationRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public Task<IReadOnlyList<AdministrationUserSummary>> ListUsersAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, MaximumListSize);
        return _repository.ListUsersAsync(search?.Trim(), safeLimit, _clock.UtcNow, cancellationToken);
    }

    public Task<AdministrationUserDetails?> GetUserAsync(
        string email,
        CancellationToken cancellationToken) =>
        _repository.GetUserDetailsAsync(
            AppUser.NormalizeEmail(email),
            _clock.UtcNow,
            cancellationToken);

    public async Task<bool> LockUserAsync(
        string email,
        string actor,
        string reason,
        CancellationToken cancellationToken)
    {
        var user = await FindUserForUpdateAsync(email, cancellationToken);
        if (user is null)
        {
            return false;
        }

        var now = _clock.UtcNow;
        user.Deactivate(now);
        await _repository.RevokeSessionsAsync(user.Id, now, cancellationToken);
        await AddAuditAsync(user, actor, "user.lock", reason, now, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnlockUserAsync(
        string email,
        string actor,
        string reason,
        CancellationToken cancellationToken)
    {
        var user = await FindUserForUpdateAsync(email, cancellationToken);
        if (user is null)
        {
            return false;
        }

        var now = _clock.UtcNow;
        user.Activate(now);
        await AddAuditAsync(user, actor, "user.unlock", reason, now, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int?> RevokeSessionsAsync(
        string email,
        string actor,
        string reason,
        CancellationToken cancellationToken)
    {
        var user = await FindUserForUpdateAsync(email, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var revokedCount = await _repository.RevokeSessionsAsync(user.Id, now, cancellationToken);
        await AddAuditAsync(user, actor, "sessions.revoke", reason, now, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return revokedCount;
    }

    public async Task<AccountDeletionResult?> DeleteUserAsync(
        string email,
        string confirmationEmail,
        string actor,
        string reason,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = AppUser.NormalizeEmail(email);
        if (!string.Equals(normalizedEmail, AppUser.NormalizeEmail(confirmationEmail), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The confirmation email must exactly match the target account.");
        }

        var user = await _repository.GetUserForUpdateAsync(normalizedEmail, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var auditEvent = CreateAudit(user, actor, "user.delete", reason, now);
        return await _repository.DeleteAccountAsync(user, auditEvent, now, cancellationToken);
    }

    private Task<AppUser?> FindUserForUpdateAsync(
        string email,
        CancellationToken cancellationToken) =>
        _repository.GetUserForUpdateAsync(AppUser.NormalizeEmail(email), cancellationToken);

    private Task AddAuditAsync(
        AppUser user,
        string actor,
        string action,
        string reason,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken) =>
        _repository.AddAuditEventAsync(
            CreateAudit(user, actor, action, reason, occurredAt),
            cancellationToken);

    private static OperatorAuditEvent CreateAudit(
        AppUser user,
        string actor,
        string action,
        string reason,
        DateTimeOffset occurredAt) =>
        OperatorAuditEvent.Create(actor, action, user.Id, user.Email, reason, occurredAt);
}
