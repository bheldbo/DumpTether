using System.Net;
using System.Text;
using DumpTether.App.Email;

namespace DumpTether.App.Notifications;

public static class NotificationEmailBuilders
{
    public static EmailMessage SharingAccepted(
        string email,
        string displayName,
        string acceptedBy,
        string resourceName,
        int resourceCount)
    {
        var subject = resourceCount == 1
            ? $"{acceptedBy} accepted your DumpTether share"
            : $"{acceptedBy} accepted {resourceCount} shared tasks";
        var body = resourceCount == 1
            ? $"{acceptedBy} accepted your invitation to {resourceName}."
            : $"{acceptedBy} accepted access to {resourceCount} tasks, including {resourceName}.";
        return Build(email, displayName, subject, "Sharing update", body, subject);
    }

    public static EmailMessage DailySummary(NotificationDigestSnapshot snapshot)
    {
        var body = $"You have {snapshot.ActiveTaskCount} active task(s). " +
            $"{snapshot.UpdatedTaskCount} changed during the last day, and " +
            $"{snapshot.OverdueFollowUpCount} follow-up(s) are overdue.";
        return Build(
            snapshot.Email,
            snapshot.DisplayName,
            "Your DumpTether daily summary",
            "Daily summary",
            body,
            body);
    }

    public static EmailMessage FollowUpReminder(NotificationDigestSnapshot snapshot)
    {
        var lines = snapshot.FollowUps
            .Select(item =>
                $"{item.Title} ({item.WorkspaceName}) - {item.FollowUpAt?.ToUniversalTime():u}")
            .ToList();
        var htmlItems = string.Join(
            string.Empty,
            lines.Select(line =>
                $"<li style=\"margin:0 0 8px;\">{WebUtility.HtmlEncode(line)}</li>"));
        var text = "Follow-ups that need attention:\n" + string.Join("\n", lines);
        return Build(
            snapshot.Email,
            snapshot.DisplayName,
            "DumpTether follow-ups need attention",
            "Follow-ups",
            $"<p style=\"margin:0 0 14px;\">You have {lines.Count} follow-up(s) due soon or overdue.</p>" +
                $"<ul style=\"margin:0;padding-left:22px;\">{htmlItems}</ul>",
            text,
            bodyIsHtml: true);
    }

    private static EmailMessage Build(
        string email,
        string displayName,
        string subject,
        string heading,
        string body,
        string text,
        bool bodyIsHtml = false)
    {
        var greeting = string.IsNullOrWhiteSpace(displayName)
            ? "there"
            : WebUtility.HtmlEncode(displayName.Trim());
        var encodedBody = bodyIsHtml ? body : WebUtility.HtmlEncode(body);
        return new EmailMessage(
            email,
            displayName,
            subject,
            $"""
            <!doctype html><html lang="en"><body style="margin:0;padding:0;background:#f3f6f7;color:#172536;font-family:Arial,sans-serif;">
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f6f7;padding:28px 12px;"><tr><td align="center">
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#fff;border:1px solid #d7e0e5;border-radius:8px;overflow:hidden;">
            <tr><td style="padding:24px 28px 14px;background:#fff4a8;border-bottom:1px solid #eadf9b;"><div style="font-size:13px;font-weight:700;color:#566451;text-transform:uppercase;">DumpTether</div><h1 style="margin:8px 0 0;font-size:25px;line-height:1.2;color:#152334;">{WebUtility.HtmlEncode(heading)}</h1></td></tr>
            <tr><td style="padding:26px 28px 30px;"><p style="margin:0 0 14px;font-size:16px;line-height:1.55;">Hi {greeting},</p><div style="font-size:16px;line-height:1.55;">{encodedBody}</div><p style="margin:22px 0 0;color:#667586;font-size:13px;line-height:1.5;">Manage these emails from Account settings.</p></td></tr>
            </table></td></tr></table></body></html>
            """,
            text);
    }
}
