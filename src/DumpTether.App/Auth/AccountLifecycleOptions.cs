namespace DumpTether.App.Auth;

public sealed class PasswordRecoveryOptions
{
    public bool Enabled { get; set; }

    public int TokenHours { get; set; } = 1;

    public string PublicBaseUrl { get; set; } = "http://localhost:5173";

    public string ResetPath { get; set; } = "/#reset-password=";
}

public sealed class AccountDeletionOptions
{
    public bool Enabled { get; set; }

    public int GraceHours { get; set; } = 48;

    public int ReminderHoursBefore { get; set; } = 24;

    public int SweepIntervalMinutes { get; set; } = 30;

    public int RecentAuthenticationMinutes { get; set; } = 10;
}
