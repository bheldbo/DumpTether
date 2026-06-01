namespace DumpTether.App.Auth;

public sealed class EmailConfirmationOptions
{
    public bool Enabled { get; set; }

    public int TokenHours { get; set; } = 24;

    public string PublicBaseUrl { get; set; } = "http://localhost:55868";

    public string ConfirmPath { get; set; } = "/api/auth/confirm-email";
}
