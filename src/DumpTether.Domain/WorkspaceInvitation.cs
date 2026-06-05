namespace DumpTether.Domain;

public sealed class WorkspaceInvitation
{
    private WorkspaceInvitation()
    {
    }

    private WorkspaceInvitation(
        Guid id,
        Guid workspaceId,
        string email,
        string normalizedEmail,
        WorkspaceMembershipRole role,
        string tokenHash,
        Guid invitedByUserId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Email = email;
        NormalizedEmail = normalizedEmail;
        Role = role;
        TokenHash = tokenHash;
        InvitedByUserId = invitedByUserId;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public WorkspaceMembershipRole Role { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public Guid InvitedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public static WorkspaceInvitation Create(
        Guid workspaceId,
        string email,
        WorkspaceMembershipRole role,
        string tokenHash,
        Guid invitedByUserId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        DomainGuards.NotEmpty(workspaceId, nameof(workspaceId));
        DomainGuards.NotEmpty(invitedByUserId, nameof(invitedByUserId));

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentException("Workspace invitation role is not valid.", nameof(role));
        }

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException(
                "Workspace invitation expiry must be after creation.",
                nameof(expiresAt));
        }

        return new WorkspaceInvitation(
            Guid.NewGuid(),
            workspaceId,
            AppUser.NormalizeEmail(email).ToLowerInvariant(),
            AppUser.NormalizeEmail(email),
            role,
            DomainGuards.NotBlank(tokenHash, nameof(tokenHash)),
            invitedByUserId,
            createdAt,
            expiresAt);
    }

    public bool IsUsable(DateTimeOffset now)
    {
        return AcceptedAt is null && RevokedAt is null && ExpiresAt > now;
    }

    public void Accept(DateTimeOffset acceptedAt)
    {
        AcceptedAt ??= acceptedAt;
    }

    public void Revoke(DateTimeOffset revokedAt)
    {
        RevokedAt ??= revokedAt;
    }
}
