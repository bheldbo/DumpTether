namespace DumpTether.App.Email;

public sealed class EmailOptions
{
    public EmailProvider Provider { get; set; } = EmailProvider.None;

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "DumpTether";

    public SmtpEmailOptions Smtp { get; set; } = new();

    public BrevoApiEmailOptions BrevoApi { get; set; } = new();
}

public sealed class SmtpEmailOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool UseAuthentication { get; set; }

    public bool EnableSsl { get; set; }
}

public sealed class BrevoApiEmailOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string Endpoint { get; set; } = "https://api.brevo.com/v3/smtp/email";
}

public enum EmailProvider
{
    None = 0,
    Smtp = 1,
    BrevoApi = 2
}
