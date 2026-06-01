namespace DumpTether.Domain;

public sealed class UserSession
{
    private UserSession()
    {
    }

    private UserSession(
        Guid id,
        Guid userId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        string sessionTokenHash,
        string? userAgent,
        string? ipAddressHash,
        string? deviceName)
    {
        Id = id;
        UserId = userId;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        LastSeenAt = createdAt;
        SessionTokenHash = sessionTokenHash;
        UserAgent = userAgent;
        IpAddressHash = ipAddressHash;
        DeviceName = deviceName;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public string SessionTokenHash { get; private set; } = string.Empty;

    public string? UserAgent { get; private set; }

    public string? IpAddressHash { get; private set; }

    public string? DeviceName { get; private set; }

    public static UserSession Create(
        Guid userId,
        string sessionTokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        string? userAgent = null,
        string? ipAddressHash = null,
        string? deviceName = null)
    {
        DomainGuards.NotEmpty(userId, nameof(userId));

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("Session expiry must be after creation.", nameof(expiresAt));
        }

        return new UserSession(
            Guid.NewGuid(),
            userId,
            createdAt,
            expiresAt,
            DomainGuards.NotBlank(sessionTokenHash, nameof(sessionTokenHash)),
            Truncate(DomainGuards.OptionalTrimmed(userAgent), 512),
            DomainGuards.OptionalTrimmed(ipAddressHash),
            Truncate(DomainGuards.OptionalTrimmed(deviceName), 120));
    }

    public bool IsUsable(DateTimeOffset now)
    {
        return RevokedAt is null && ExpiresAt > now;
    }

    public void MarkSeen(DateTimeOffset seenAt)
    {
        LastSeenAt = seenAt;
    }

    public void Revoke(DateTimeOffset revokedAt)
    {
        RevokedAt ??= revokedAt;
        LastSeenAt = revokedAt;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        return value is null || value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}
