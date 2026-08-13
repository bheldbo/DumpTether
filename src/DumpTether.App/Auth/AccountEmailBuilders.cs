using System.Net;
using DumpTether.App.Email;

namespace DumpTether.App.Auth;

public static class AccountEmailBuilders
{
    public static EmailMessage PasswordReset(
        string email,
        string? displayName,
        string resetLink,
        int tokenHours)
    {
        var encodedLink = WebUtility.HtmlEncode(resetLink);
        var greeting = Greeting(displayName);
        var hours = Math.Max(1, tokenHours);
        return Build(
            email,
            displayName,
            "Reset your DumpTether password",
            "Reset your password",
            $"Hi {greeting},",
            "Someone requested a password reset for your DumpTether account.",
            "Reset password",
            encodedLink,
            $"This single-use link expires in {hours} hour{(hours == 1 ? string.Empty : "s")}. If you did not request it, you can ignore this email.",
            $"Reset your DumpTether password within {hours} hour(s): {resetLink}");
    }

    public static EmailMessage AccountDeletionScheduled(
        string email,
        string? displayName,
        DateTimeOffset scheduledFor)
    {
        var formatted = scheduledFor.ToUniversalTime().ToString("u");
        return Build(
            email,
            displayName,
            "DumpTether account deletion scheduled",
            "Account deletion scheduled",
            $"Hi {Greeting(displayName)},",
            $"Your DumpTether account is scheduled for deletion at {formatted}. You can cancel from Account settings before then.",
            null,
            null,
            "This removes your account and owned DumpTether data. Backups may remain until their normal retention expires.",
            $"Your DumpTether account is scheduled for deletion at {formatted}. Cancel from Account settings before then.");
    }

    public static EmailMessage AccountDeletionReminder(
        string email,
        string? displayName,
        DateTimeOffset scheduledFor)
    {
        var formatted = scheduledFor.ToUniversalTime().ToString("u");
        return Build(
            email,
            displayName,
            "Your DumpTether account will be deleted in about 24 hours",
            "Account deletion reminder",
            $"Hi {Greeting(displayName)},",
            $"Your DumpTether account is still scheduled for deletion at {formatted}.",
            null,
            null,
            "Open Account settings before that time if you want to cancel the deletion.",
            $"Your DumpTether account is scheduled for deletion at {formatted}. Cancel from Account settings before then.");
    }

    private static EmailMessage Build(
        string email,
        string? displayName,
        string subject,
        string heading,
        string greeting,
        string body,
        string? actionLabel,
        string? actionLink,
        string footnote,
        string text)
    {
        var action = actionLabel is null || actionLink is null
            ? string.Empty
            : $"<p style=\"margin:0 0 24px;\"><a href=\"{actionLink}\" style=\"display:inline-block;padding:12px 20px;border-radius:999px;background:#174f4b;color:#fff;font-size:16px;font-weight:700;text-decoration:none;\">{WebUtility.HtmlEncode(actionLabel)}</a></p>";
        return new EmailMessage(
            email,
            displayName,
            subject,
            $"""
            <!doctype html><html lang="en"><body style="margin:0;padding:0;background:#f3f6f7;color:#172536;font-family:Arial,sans-serif;">
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f6f7;padding:28px 12px;"><tr><td align="center">
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#fff;border:1px solid #d7e0e5;border-radius:8px;overflow:hidden;">
            <tr><td style="padding:24px 28px 14px;background:#fff4a8;border-bottom:1px solid #eadf9b;"><div style="font-size:13px;font-weight:700;color:#566451;text-transform:uppercase;">DumpTether</div><h1 style="margin:8px 0 0;font-size:25px;line-height:1.2;color:#152334;">{WebUtility.HtmlEncode(heading)}</h1></td></tr>
            <tr><td style="padding:26px 28px 30px;"><p style="margin:0 0 14px;font-size:16px;line-height:1.55;">{greeting}</p><p style="margin:0 0 22px;font-size:16px;line-height:1.55;">{WebUtility.HtmlEncode(body)}</p>{action}<p style="margin:0;color:#667586;font-size:13px;line-height:1.5;">{WebUtility.HtmlEncode(footnote)}</p></td></tr>
            </table></td></tr></table></body></html>
            """,
            text);
    }

    private static string Greeting(string? displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? "there" : WebUtility.HtmlEncode(displayName.Trim());
}
