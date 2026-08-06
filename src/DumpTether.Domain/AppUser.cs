namespace DumpTether.Domain;

public sealed class AppUser
{
    private readonly List<UserSession> _sessions = [];
    private readonly List<WorkspaceMembership> _workspaceMemberships = [];

    private AppUser()
    {
    }

    private AppUser(
        Guid id,
        string email,
        string normalizedEmail,
        string displayName,
        string passwordHash,
        DateTimeOffset createdAt,
        DateTimeOffset? emailConfirmedAt)
    {
        Id = id;
        Email = email;
        NormalizedEmail = normalizedEmail;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        EmailConfirmedAt = emailConfirmedAt;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public DateTimeOffset? EmailConfirmedAt { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<UserSession> Sessions => _sessions.AsReadOnly();

    public IReadOnlyCollection<WorkspaceMembership> WorkspaceMemberships =>
        _workspaceMemberships.AsReadOnly();

    public static AppUser Create(
        string email,
        string? displayName,
        string passwordHash,
        DateTimeOffset createdAt,
        bool emailIsConfirmed = true)
    {
        var normalizedEmail = NormalizeEmail(email);
        var trimmedDisplayName = DomainGuards.OptionalTrimmed(displayName) ??
            normalizedEmail.Split('@')[0];

        return new AppUser(
            Guid.NewGuid(),
            NormalizeEmailForStorage(email),
            normalizedEmail,
            trimmedDisplayName,
            DomainGuards.NotBlank(passwordHash, nameof(passwordHash)),
            createdAt,
            emailIsConfirmed ? createdAt : null);
    }

    public static string NormalizeEmail(string email)
    {
        return NormalizeEmailForStorage(email).ToUpperInvariant();
    }

    public void MarkLoggedIn(DateTimeOffset loggedInAt)
    {
        LastLoginAt = loggedInAt;
        UpdatedAt = loggedInAt;
    }

    public void MarkEmailConfirmed(DateTimeOffset confirmedAt)
    {
        EmailConfirmedAt ??= confirmedAt;
        UpdatedAt = confirmedAt;
    }

    public void Deactivate(DateTimeOffset deactivatedAt)
    {
        IsActive = false;
        UpdatedAt = deactivatedAt;
    }

    public void Activate(DateTimeOffset activatedAt)
    {
        IsActive = true;
        UpdatedAt = activatedAt;
    }

    private static string NormalizeEmailForStorage(string email)
    {
        var normalizedEmail = DomainGuards.NotBlank(email, nameof(email));

        if (normalizedEmail.Length > 320 ||
            normalizedEmail.Count(character => character == '@') != 1 ||
            normalizedEmail.StartsWith('@') ||
            normalizedEmail.EndsWith('@'))
        {
            throw new ArgumentException("Email address is not valid.", nameof(email));
        }

        return normalizedEmail;
    }
}
