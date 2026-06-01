namespace DumpTether.App.Email;

public sealed record EmailMessage(
    string ToEmail,
    string? ToName,
    string Subject,
    string HtmlContent,
    string? TextContent = null);
