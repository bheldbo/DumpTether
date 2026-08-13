using DumpTether.Domain;
using Xunit;

namespace DumpTether.Domain.Tests;

public sealed class AccountLifecycleTests
{
    [Fact]
    public void PasswordResetToken_IsUsableUntilItExpiresOrIsUsed()
    {
        var now = DateTimeOffset.UtcNow;
        var token = PasswordResetToken.Create(
            Guid.NewGuid(),
            "hashed-token",
            now,
            now.AddHours(1));

        Assert.True(token.IsUsable(now.AddMinutes(59)));
        Assert.False(token.IsUsable(now.AddHours(1)));

        token.MarkUsed(now.AddMinutes(10));

        Assert.False(token.IsUsable(now.AddMinutes(11)));
    }

    [Fact]
    public void AccountDeletionRequest_TracksReminderAndDeletionClaims()
    {
        var now = DateTimeOffset.UtcNow;
        var request = AccountDeletionRequest.Create(
            Guid.NewGuid(),
            now,
            now.AddHours(24),
            now.AddHours(48));

        request.ClaimReminder(now.AddHours(24));
        request.MarkReminderSent(now.AddHours(24));
        request.Claim(now.AddHours(48));

        Assert.NotNull(request.ReminderSentAt);
        Assert.Null(request.ReminderClaimedAt);
        Assert.Equal(AccountDeletionRequestState.Deleting, request.State);
        Assert.NotNull(request.ClaimedAt);

        request.ReleaseClaim();

        Assert.Equal(AccountDeletionRequestState.Pending, request.State);
        Assert.Null(request.ClaimedAt);
    }
}
