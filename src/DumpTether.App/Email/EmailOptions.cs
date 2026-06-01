namespace DumpTether.App.Email;

public sealed class EmailOptions
{
    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "DumpTether";

    public SmtpEmailOptions Smtp { get; set; } = new();

    public BrevoApiEmailOptions BrevoApi { get; set; } = new();
}

public sealed class SmtpEmailOptions
{
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class BrevoApiEmailOptions
{
    public bool Enabled { get; set; }

    public string ApiKey { get; set; } = string.Empty;

    public string Endpoint { get; set; } = "https://api.brevo.com/v3/smtp/email";
}
