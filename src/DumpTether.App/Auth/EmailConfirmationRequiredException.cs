namespace DumpTether.App.Auth;

public sealed class EmailConfirmationRequiredException : Exception
{
    public EmailConfirmationRequiredException()
        : base("Email confirmation is required.")
    {
    }
}
