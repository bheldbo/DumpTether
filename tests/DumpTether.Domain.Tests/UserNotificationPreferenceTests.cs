using DumpTether.Domain;
using Xunit;

namespace DumpTether.Domain.Tests;

public sealed class UserNotificationPreferenceTests
{
    [Fact]
    public void Create_DefaultsEveryEmailToOff()
    {
        var preference = UserNotificationPreference.Create(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        Assert.False(preference.SharingActivityEmailEnabled);
        Assert.False(preference.DailySummaryEmailEnabled);
        Assert.False(preference.FollowUpReminderEmailEnabled);
    }

    [Fact]
    public void Update_ChangesOptInPreferencesAndTimestamp()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var updatedAt = createdAt.AddMinutes(5);
        var preference = UserNotificationPreference.Create(Guid.NewGuid(), createdAt);

        var changed = preference.Update(
            sharingActivityEmailEnabled: true,
            dailySummaryEmailEnabled: true,
            followUpReminderEmailEnabled: true,
            updatedAt);

        Assert.True(changed);
        Assert.True(preference.SharingActivityEmailEnabled);
        Assert.True(preference.DailySummaryEmailEnabled);
        Assert.True(preference.FollowUpReminderEmailEnabled);
        Assert.Equal(updatedAt, preference.UpdatedAt);
    }
}
