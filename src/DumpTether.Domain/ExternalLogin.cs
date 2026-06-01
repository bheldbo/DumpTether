namespace DumpTether.Domain;

public sealed class ExternalLogin
{
    private ExternalLogin()
    {
    }

    private ExternalLogin(
        Guid id,
        Guid userId,
        string provider,
        string providerUserId,
        string emailAtLogin,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        Provider = provider;
        ProviderUserId = providerUserId;
        EmailAtLogin = emailAtLogin;
        CreatedAt = createdAt;
        LastUsedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string ProviderUserId { get; private set; } = string.Empty;

    public string EmailAtLogin { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastUsedAt { get; private set; }

    public static ExternalLogin Create(
        Guid userId,
        string provider,
        string providerUserId,
        string emailAtLogin,
        DateTimeOffset createdAt)
    {
        DomainGuards.NotEmpty(userId, nameof(userId));

        return new ExternalLogin(
            Guid.NewGuid(),
            userId,
            NormalizeProvider(provider),
            DomainGuards.NotBlank(providerUserId, nameof(providerUserId)),
            AppUser.NormalizeEmail(emailAtLogin),
            createdAt);
    }

    public static string NormalizeProvider(string provider)
    {
        return DomainGuards.NotBlank(provider, nameof(provider)).Trim().ToLowerInvariant();
    }

    public void MarkUsed(DateTimeOffset usedAt, string emailAtLogin)
    {
        EmailAtLogin = AppUser.NormalizeEmail(emailAtLogin);
        LastUsedAt = usedAt;
    }
}
