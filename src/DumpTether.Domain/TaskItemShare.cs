namespace DumpTether.Domain;

public sealed class TaskItemShare
{
    private TaskItemShare()
    {
    }

    private TaskItemShare(
        Guid id,
        Guid workspaceId,
        Guid taskItemId,
        string email,
        string normalizedEmail,
        Guid? sharedWithUserId,
        Guid sharedByUserId,
        TaskItemShareRole role,
        string? tokenHash,
        DateTimeOffset createdAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        TaskItemId = taskItemId;
        Email = email;
        NormalizedEmail = normalizedEmail;
        SharedWithUserId = sharedWithUserId;
        SharedByUserId = sharedByUserId;
        Role = role;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public Guid TaskItemId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public Guid? SharedWithUserId { get; private set; }

    public Guid SharedByUserId { get; private set; }

    public TaskItemShareRole Role { get; private set; }

    public string? TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public static TaskItemShare Create(
        Guid workspaceId,
        Guid taskItemId,
        string email,
        Guid? sharedWithUserId,
        Guid sharedByUserId,
        TaskItemShareRole role,
        string? tokenHash,
        DateTimeOffset? expiresAt,
        DateTimeOffset createdAt)
    {
        DomainGuards.NotEmpty(workspaceId, nameof(workspaceId));
        DomainGuards.NotEmpty(taskItemId, nameof(taskItemId));
        DomainGuards.NotEmpty(sharedByUserId, nameof(sharedByUserId));

        if (sharedWithUserId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(sharedWithUserId));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentException("Task share role is not valid.", nameof(role));
        }

        if (!string.IsNullOrWhiteSpace(tokenHash) &&
            (!expiresAt.HasValue || expiresAt <= createdAt))
        {
            throw new ArgumentException(
                "Task share expiry must be after creation when a token is provided.",
                nameof(expiresAt));
        }

        var share = new TaskItemShare(
            Guid.NewGuid(),
            workspaceId,
            taskItemId,
            AppUser.NormalizeEmail(email).ToLowerInvariant(),
            AppUser.NormalizeEmail(email),
            sharedWithUserId,
            sharedByUserId,
            role,
            string.IsNullOrWhiteSpace(tokenHash) ? null : tokenHash,
            createdAt);

        share.ExpiresAt = expiresAt;
        share.AcceptedAt = string.IsNullOrWhiteSpace(tokenHash) ? createdAt : null;

        return share;
    }

    public bool IsPending => RevokedAt is null &&
        AcceptedAt is null &&
        !string.IsNullOrWhiteSpace(TokenHash);

    public bool IsActive => RevokedAt is null &&
        (AcceptedAt is not null || string.IsNullOrWhiteSpace(TokenHash));

    public bool IsUsable(DateTimeOffset now)
    {
        return IsPending &&
            !string.IsNullOrWhiteSpace(TokenHash) &&
            ExpiresAt.HasValue &&
            ExpiresAt.Value > now;
    }

    public bool MatchesUser(Guid userId, string normalizedEmail)
    {
        return IsActive &&
            (SharedWithUserId == userId ||
                string.Equals(NormalizedEmail, normalizedEmail, StringComparison.Ordinal));
    }

    public void LinkUser(Guid userId)
    {
        DomainGuards.NotEmpty(userId, nameof(userId));
        SharedWithUserId ??= userId;
    }

    public void Accept(Guid userId, DateTimeOffset acceptedAt)
    {
        DomainGuards.NotEmpty(userId, nameof(userId));

        SharedWithUserId ??= userId;
        AcceptedAt ??= acceptedAt;
    }

    public void Revoke(DateTimeOffset revokedAt)
    {
        RevokedAt ??= revokedAt;
    }
}
