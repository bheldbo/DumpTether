namespace DumpTether.App.Auth;

public sealed class LegalOptions
{
    public bool RequireAcceptance { get; set; }

    public string TermsVersion { get; set; } = string.Empty;

    public string PrivacyNoticeVersion { get; set; } = string.Empty;

    public string OperatorName { get; set; } = string.Empty;

    public string PrivacyContactEmail { get; set; } = string.Empty;
}
