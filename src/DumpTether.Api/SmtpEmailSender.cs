using System.Net;
using System.Net.Mail;
using DumpTether.App.Email;
using Microsoft.Extensions.Options;

namespace DumpTether.Api;

internal sealed class SmtpEmailSender : IEmailSender
{
    private readonly IOptions<EmailOptions> _emailOptions;

    public SmtpEmailSender(IOptions<EmailOptions> emailOptions)
    {
        _emailOptions = emailOptions;
    }

    public async Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var options = _emailOptions.Value;
        var smtp = options.Smtp;

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(options.FromEmail, options.FromName),
            Subject = message.Subject,
            Body = string.IsNullOrWhiteSpace(message.HtmlContent)
                ? message.TextContent
                : message.HtmlContent,
            IsBodyHtml = !string.IsNullOrWhiteSpace(message.HtmlContent)
        };
        mailMessage.To.Add(string.IsNullOrWhiteSpace(message.ToName)
            ? new MailAddress(message.ToEmail)
            : new MailAddress(message.ToEmail, message.ToName));

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl,
            UseDefaultCredentials = false
        };

        if (smtp.UseAuthentication)
        {
            client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);
        }

        try
        {
            await client.SendMailAsync(mailMessage).WaitAsync(cancellationToken);
        }
        catch (Exception exception) when (
            exception is SmtpException or InvalidOperationException)
        {
            throw new EmailDeliveryException(
                $"The configured SMTP server could not send the email: {exception.Message}");
        }
    }
}
