using System.Net;

namespace DumpTether.Api;

internal static class EmailConfirmationPageRenderer
{
    public static string Success(string email, string returnUrl) => Render(
        "Email confirmed",
        $"{WebUtility.HtmlEncode(email)} is ready to use with DumpTether.",
        returnUrl,
        autoRedirect: true,
        isSuccess: true);

    public static string Failure(string returnUrl) => Render(
        "This confirmation link is not valid",
        "The link may have expired or already been used. Return to DumpTether and register again if you still need an account.",
        returnUrl,
        autoRedirect: false,
        isSuccess: false);

    private static string Render(
        string title,
        string message,
        string returnUrl,
        bool autoRedirect,
        bool isSuccess)
    {
        var encodedUrl = WebUtility.HtmlEncode(returnUrl);
        var encodedTitle = WebUtility.HtmlEncode(title);
        var accent = isSuccess ? "#174f4b" : "#9a332f";
        var status = isSuccess ? "Confirmed" : "Link unavailable";
        var redirect = autoRedirect
            ? $"<meta http-equiv=\"refresh\" content=\"6;url={encodedUrl}\">"
            : string.Empty;

        return $"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          {redirect}
          <title>DumpTether - {encodedTitle}</title>
        </head>
        <body style="margin:0;background:#eef3f5;color:#172536;font-family:system-ui,-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;">
          <main style="box-sizing:border-box;min-height:100vh;display:grid;place-items:center;padding:24px;">
            <section style="box-sizing:border-box;width:min(520px,100%);overflow:hidden;border:1px solid #d4dee4;border-radius:8px;background:#fff;box-shadow:0 18px 50px rgba(31,48,62,.12);">
              <header style="padding:22px 26px;background:#fff4a8;border-bottom:1px solid #eadf9b;">
                <div style="font-size:13px;font-weight:750;color:#566451;text-transform:uppercase;">DumpTether</div>
                <div style="margin-top:8px;color:{accent};font-size:14px;font-weight:750;">{status}</div>
              </header>
              <div style="padding:28px 26px 30px;">
                <h1 style="margin:0 0 12px;font-size:27px;line-height:1.2;">{encodedTitle}</h1>
                <p style="margin:0 0 24px;color:#5f6f80;font-size:16px;line-height:1.55;">{message}</p>
                <a href="{encodedUrl}" style="display:inline-block;padding:12px 20px;border-radius:999px;background:{accent};color:#fff;font-weight:750;text-decoration:none;">Return to DumpTether login</a>
                {(autoRedirect ? "<p style=\"margin:16px 0 0;color:#7a8794;font-size:13px;\">Returning automatically in a few seconds.</p>" : string.Empty)}
              </div>
            </section>
          </main>
        </body>
        </html>
        """;
    }
}
