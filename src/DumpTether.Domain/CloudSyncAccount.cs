namespace DumpTether.Domain;

public sealed class CloudSyncAccount
{
    private CloudSyncAccount()
    {
    }

    private CloudSyncAccount(
        Guid id,
        Guid userId,
        string cloudApiBaseUrl,
        Guid cloudUserId,
        string cloudEmail,
        string cloudDisplayName,
        string protectedSessionToken,
        DateTimeOffset sessionExpiresAt,
        DateTimeOffset connectedAt)
    {
        Id = id;
        UserId = userId;
        CloudApiBaseUrl = cloudApiBaseUrl;
        CloudUserId = cloudUserId;
        CloudEmail = cloudEmail;
        CloudDisplayName = cloudDisplayName;
        ProtectedSessionToken = protectedSessionToken;
        SessionExpiresAt = sessionExpiresAt;
        ConnectedAt = connectedAt;
        UpdatedAt = connectedAt;
        LastVerifiedAt = connectedAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string CloudApiBaseUrl { get; private set; } = string.Empty;

    public Guid CloudUserId { get; private set; }

    public string CloudEmail { get; private set; } = string.Empty;

    public string CloudDisplayName { get; private set; } = string.Empty;

    public string ProtectedSessionToken { get; private set; } = string.Empty;

    public DateTimeOffset SessionExpiresAt { get; private set; }

    public DateTimeOffset ConnectedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? LastVerifiedAt { get; private set; }

    public DateTimeOffset? DisconnectedAt { get; private set; }

    public static CloudSyncAccount Create(
        Guid userId,
        string cloudApiBaseUrl,
        Guid cloudUserId,
        string cloudEmail,
        string cloudDisplayName,
        string protectedSessionToken,
        DateTimeOffset sessionExpiresAt,
        DateTimeOffset connectedAt)
    {
        DomainGuards.NotEmpty(userId, nameof(userId));
        DomainGuards.NotEmpty(cloudUserId, nameof(cloudUserId));

        if (sessionExpiresAt <= connectedAt)
        {
            throw new ArgumentException("Cloud session expiry must be after connection time.", nameof(sessionExpiresAt));
        }

        return new CloudSyncAccount(
            Guid.NewGuid(),
            userId,
            NormalizeCloudApiBaseUrl(cloudApiBaseUrl),
            cloudUserId,
            Truncate(DomainGuards.NotBlank(cloudEmail, nameof(cloudEmail)).Trim(), 320),
            Truncate(DomainGuards.NotBlank(cloudDisplayName, nameof(cloudDisplayName)).Trim(), 160),
            DomainGuards.NotBlank(protectedSessionToken, nameof(protectedSessionToken)),
            sessionExpiresAt,
            connectedAt);
    }

    public bool HasUsableSession(DateTimeOffset now)
    {
        return DisconnectedAt is null && SessionExpiresAt > now;
    }

    public void ReplaceConnection(
        string cloudApiBaseUrl,
        Guid cloudUserId,
        string cloudEmail,
        string cloudDisplayName,
        string protectedSessionToken,
        DateTimeOffset sessionExpiresAt,
        DateTimeOffset updatedAt)
    {
        DomainGuards.NotEmpty(cloudUserId, nameof(cloudUserId));

        if (sessionExpiresAt <= updatedAt)
        {
            throw new ArgumentException("Cloud session expiry must be after update time.", nameof(sessionExpiresAt));
        }

        CloudApiBaseUrl = NormalizeCloudApiBaseUrl(cloudApiBaseUrl);
        CloudUserId = cloudUserId;
        CloudEmail = Truncate(DomainGuards.NotBlank(cloudEmail, nameof(cloudEmail)).Trim(), 320);
        CloudDisplayName = Truncate(DomainGuards.NotBlank(cloudDisplayName, nameof(cloudDisplayName)).Trim(), 160);
        ProtectedSessionToken = DomainGuards.NotBlank(protectedSessionToken, nameof(protectedSessionToken));
        SessionExpiresAt = sessionExpiresAt;
        UpdatedAt = updatedAt;
        LastVerifiedAt = updatedAt;
        DisconnectedAt = null;
    }

    public void MarkVerified(
        Guid cloudUserId,
        string cloudEmail,
        string cloudDisplayName,
        DateTimeOffset verifiedAt)
    {
        if (cloudUserId != CloudUserId)
        {
            throw new InvalidOperationException("Connected cloud account returned a different cloud user.");
        }

        CloudEmail = Truncate(DomainGuards.NotBlank(cloudEmail, nameof(cloudEmail)).Trim(), 320);
        CloudDisplayName = Truncate(DomainGuards.NotBlank(cloudDisplayName, nameof(cloudDisplayName)).Trim(), 160);
        LastVerifiedAt = verifiedAt;
        UpdatedAt = verifiedAt;
    }

    public void Disconnect(DateTimeOffset disconnectedAt)
    {
        DisconnectedAt ??= disconnectedAt;
        ProtectedSessionToken = string.Empty;
        UpdatedAt = disconnectedAt;
    }

    public static string NormalizeCloudApiBaseUrl(string cloudApiBaseUrl)
    {
        if (!Uri.TryCreate(DomainGuards.NotBlank(cloudApiBaseUrl, nameof(cloudApiBaseUrl)).Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            throw new ArgumentException(
                "Cloud API base URL must be an absolute HTTP(S) URL without credentials.",
                nameof(cloudApiBaseUrl));
        }

        if (uri.Scheme == Uri.UriSchemeHttp && !IsLoopbackHost(uri))
        {
            throw new ArgumentException(
                "Cloud API base URL must use HTTPS unless it targets the local development machine.",
                nameof(cloudApiBaseUrl));
        }

        return uri.AbsoluteUri.TrimEnd('/');
    }

    private static bool IsLoopbackHost(Uri uri) =>
        uri.IsLoopback ||
        uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
