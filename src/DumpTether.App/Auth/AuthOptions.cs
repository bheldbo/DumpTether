namespace DumpTether.App.Auth;

public enum AuthSignupMode
{
    Open = 1,
    Whitelist = 2,
    InviteOnly = 3,
    Closed = 4
}

public sealed class AuthOptions
{
    public bool RequireAuthentication { get; set; } = true;

    public bool AllowGuestSessions { get; set; } = true;

    public AuthSignupMode SignupMode { get; set; } = AuthSignupMode.Open;

    public string[] SignupWhitelistEmails { get; set; } = [];

    public string[] SignupWhitelistDomains { get; set; } = [];

    public string[] SignupInviteCodes { get; set; } = [];

    public bool EnableDevelopmentLogin { get; set; }

    public string DevelopmentEmail { get; set; } = "dev@dumptether.local";

    public string DevelopmentPassword { get; set; } = "dumptether-dev-password";

    public string DevelopmentDisplayName { get; set; } = "Dev User";

    public int SessionDays { get; set; } = 30;

    public int SessionCleanupDays { get; set; } = 90;

    public int SessionCleanupIntervalHours { get; set; } = 24;
}
