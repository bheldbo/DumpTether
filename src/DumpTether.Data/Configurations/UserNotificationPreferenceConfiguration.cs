using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class UserNotificationPreferenceConfiguration :
    IEntityTypeConfiguration<UserNotificationPreference>
{
    public void Configure(EntityTypeBuilder<UserNotificationPreference> builder)
    {
        builder.ToTable("user_notification_preferences");
        builder.HasKey(preference => preference.UserId);

        builder.Property(preference => preference.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever();
        builder.Property(preference => preference.SharingActivityEmailEnabled)
            .HasColumnName("sharing_activity_email_enabled")
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(preference => preference.DailySummaryEmailEnabled)
            .HasColumnName("daily_summary_email_enabled")
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(preference => preference.FollowUpReminderEmailEnabled)
            .HasColumnName("follow_up_reminder_email_enabled")
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(preference => preference.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(preference => preference.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
        builder.Property(preference => preference.DailySummaryClaimedAt)
            .HasColumnName("daily_summary_claimed_at");
        builder.Property(preference => preference.LastDailySummarySentAt)
            .HasColumnName("last_daily_summary_sent_at");
        builder.Property(preference => preference.FollowUpReminderClaimedAt)
            .HasColumnName("follow_up_reminder_claimed_at");
        builder.Property(preference => preference.LastFollowUpReminderSentAt)
            .HasColumnName("last_follow_up_reminder_sent_at");

        builder.HasOne<AppUser>()
            .WithOne()
            .HasForeignKey<UserNotificationPreference>(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
