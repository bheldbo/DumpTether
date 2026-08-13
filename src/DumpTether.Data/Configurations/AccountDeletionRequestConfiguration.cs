using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DumpTether.Data.Configurations;

internal sealed class AccountDeletionRequestConfiguration : IEntityTypeConfiguration<AccountDeletionRequest>
{
    public void Configure(EntityTypeBuilder<AccountDeletionRequest> builder)
    {
        builder.ToTable("account_deletion_requests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(request => request.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(request => request.RequestedAt).HasColumnName("requested_at").IsRequired();
        builder.Property(request => request.ReminderDueAt).HasColumnName("reminder_due_at").IsRequired();
        builder.Property(request => request.ScheduledFor).HasColumnName("scheduled_for").IsRequired();
        builder.Property(request => request.ReminderSentAt).HasColumnName("reminder_sent_at");
        builder.Property(request => request.ReminderClaimedAt).HasColumnName("reminder_claimed_at");
        builder.Property(request => request.State).HasColumnName("state").IsRequired();
        builder.Property(request => request.ClaimedAt).HasColumnName("claimed_at");
        builder.HasIndex(request => request.UserId).IsUnique();
        builder.HasIndex(request => new { request.State, request.ScheduledFor });
        builder.HasIndex(request => new { request.State, request.ReminderDueAt, request.ReminderSentAt });
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(request => request.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
