namespace DumpTether.App.Notifications;

public sealed class NotificationOptions
{
    public bool Enabled { get; set; }

    public int SweepIntervalMinutes { get; set; } = 15;

    public int DailyDigestHourUtc { get; set; } = 7;

    public int FollowUpWindowHours { get; set; } = 24;
}
