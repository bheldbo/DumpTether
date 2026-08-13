namespace DumpTether.Domain;

public sealed class PasswordResetToken
{
    private PasswordResetToken()
    {
    }

    private PasswordResetToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public static PasswordResetToken Create(
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        DomainGuards.NotEmpty(userId, nameof(userId));
        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("Password reset token expiry must be after creation.", nameof(expiresAt));
        }

        return new PasswordResetToken(
            Guid.NewGuid(),
            userId,
            DomainGuards.NotBlank(tokenHash, nameof(tokenHash)),
            createdAt,
            expiresAt);
    }

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && ExpiresAt > now;

    public void MarkUsed(DateTimeOffset usedAt) => UsedAt ??= usedAt;
}
