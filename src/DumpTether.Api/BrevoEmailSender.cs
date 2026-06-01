using System.Net;
using System.Net.Http.Json;
using DumpTether.App.Email;
using Microsoft.Extensions.Options;

namespace DumpTether.Api;

internal sealed class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<EmailOptions> _emailOptions;

    public BrevoEmailSender(
        HttpClient httpClient,
        IOptions<EmailOptions> emailOptions)
    {
        _httpClient = httpClient;
        _emailOptions = emailOptions;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var options = _emailOptions.Value;

        if (!options.BrevoApi.Enabled)
        {
            throw new EmailDeliveryException("Brevo API email is not enabled.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            string.IsNullOrWhiteSpace(options.BrevoApi.Endpoint)
                ? "https://api.brevo.com/v3/smtp/email"
                : options.BrevoApi.Endpoint);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Add("api-key", options.BrevoApi.ApiKey);
        request.Content = JsonContent.Create(new
        {
            sender = new
            {
                name = string.IsNullOrWhiteSpace(options.FromName) ? "DumpTether" : options.FromName,
                email = options.FromEmail
            },
            to = new[]
            {
                new
                {
                    email = message.ToEmail,
                    name = message.ToName
                }
            },
            subject = message.Subject,
            htmlContent = message.HtmlContent,
            textContent = message.TextContent
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var messagePrefix = response.StatusCode is HttpStatusCode.TooManyRequests
            ? "Email provider limit was reached."
            : "Email provider rejected the message.";

        throw new EmailDeliveryException($"{messagePrefix} Brevo status {(int)response.StatusCode}. {body}");
    }
}
