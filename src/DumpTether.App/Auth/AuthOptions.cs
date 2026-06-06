namespace DumpTether.App.Auth;

public sealed class AuthOptions
{
    public bool RequireAuthentication { get; set; } = true;

    public bool AllowGuestSessions { get; set; } = true;

    public bool EnableDevelopmentLogin { get; set; }

    public string DevelopmentEmail { get; set; } = "dev@dumptether.local";

    public string DevelopmentPassword { get; set; } = "dumptether-dev-password";

    public string DevelopmentDisplayName { get; set; } = "Dev User";

    public int SessionDays { get; set; } = 30;

    public int SessionCleanupDays { get; set; } = 90;

    public int SessionCleanupIntervalHours { get; set; } = 24;
}
