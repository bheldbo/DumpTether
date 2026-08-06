using System.Net;
using DumpTether.App.Email;

namespace DumpTether.App.Auth;

internal static class EmailConfirmationEmailBuilder
{
    public static EmailMessage Build(
        string email,
        string? displayName,
        string confirmationLink,
        int tokenHours)
    {
        var encodedLink = WebUtility.HtmlEncode(confirmationLink);
        var greetingName = string.IsNullOrWhiteSpace(displayName)
            ? "there"
            : WebUtility.HtmlEncode(displayName.Trim());
        var hours = Math.Max(1, tokenHours);

        return new EmailMessage(
            email,
            displayName,
            "Confirm your DumpTether email",
            $"""
            <!doctype html>
            <html lang="en">
            <body style="margin:0;padding:0;background:#f3f6f7;color:#172536;font-family:Arial,sans-serif;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f6f7;padding:28px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#ffffff;border:1px solid #d7e0e5;border-radius:8px;overflow:hidden;">
                      <tr>
                        <td style="padding:24px 28px 14px;background:#fff4a8;border-bottom:1px solid #eadf9b;">
                          <div style="font-size:13px;font-weight:700;color:#566451;text-transform:uppercase;">DumpTether</div>
                          <h1 style="margin:8px 0 0;font-size:25px;line-height:1.2;color:#152334;">Confirm your email</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:26px 28px 30px;">
                          <p style="margin:0 0 14px;font-size:16px;line-height:1.55;">Hi {greetingName},</p>
                          <p style="margin:0 0 22px;font-size:16px;line-height:1.55;">Confirm your email to finish creating your DumpTether account.</p>
                          <p style="margin:0 0 24px;">
                            <a href="{encodedLink}" style="display:inline-block;padding:12px 20px;border-radius:999px;background:#174f4b;color:#ffffff;font-size:16px;font-weight:700;text-decoration:none;">Confirm email</a>
                          </p>
                          <p style="margin:0 0 10px;color:#667586;font-size:13px;line-height:1.5;">This link expires in {hours} hours. If you did not create this account, you can ignore this email.</p>
                          <p style="margin:18px 0 6px;color:#667586;font-size:12px;line-height:1.45;">If the button does not work, open this link:</p>
                          <p style="margin:0;overflow-wrap:anywhere;font-size:12px;line-height:1.45;"><a href="{encodedLink}" style="color:#175e59;">{encodedLink}</a></p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """,
            $"Welcome to DumpTether. Confirm your email within {hours} hours: {confirmationLink}");
    }
}
