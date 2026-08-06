using DumpTether.App.Administration;

namespace DumpTether.Admin;

internal sealed class AdminCommandRunner
{
    private readonly IAdministrationService _service;

    public AdminCommandRunner(IAdministrationService service)
    {
        _service = service;
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                ShowHelp();
                return 0;
            }

            return (args[0].ToLowerInvariant(), args.ElementAtOrDefault(1)?.ToLowerInvariant()) switch
            {
                ("users", "list") => await ListUsersAsync(args, cancellationToken),
                ("users", "show") => await ShowUserAsync(args, cancellationToken),
                ("users", "lock") => await LockUserAsync(args, cancellationToken),
                ("users", "unlock") => await UnlockUserAsync(args, cancellationToken),
                ("users", "delete") => await DeleteUserAsync(args, cancellationToken),
                ("sessions", "revoke") => await RevokeSessionsAsync(args, cancellationToken),
                _ => UnknownCommand()
            };
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"Invalid input: {exception.Message}");
            return 2;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine($"Operation refused: {exception.Message}");
            return 3;
        }
    }

    private async Task<int> ListUsersAsync(string[] args, CancellationToken cancellationToken)
    {
        var search = GetOption(args, "--search");
        var limit = int.TryParse(GetOption(args, "--limit"), out var parsedLimit) ? parsedLimit : 100;
        var users = await _service.ListUsersAsync(search, limit, cancellationToken);

        Console.WriteLine("ACTIVE  CONFIRMED  SESSIONS  BOARDS  CREATED                 EMAIL");
        foreach (var user in users)
        {
            Console.WriteLine(
                $"{YesNo(user.IsActive),-6}  {YesNo(user.EmailConfirmedAt.HasValue),-9}  " +
                $"{user.ActiveSessionCount,8}  {user.OwnedBoardCount,6}  " +
                $"{user.CreatedAt:u}  {user.Email}");
        }

        Console.WriteLine($"{users.Count} user(s).");
        return 0;
    }

    private async Task<int> ShowUserAsync(string[] args, CancellationToken cancellationToken)
    {
        var email = RequirePositional(args, 2, "email");
        var details = await _service.GetUserAsync(email, cancellationToken);
        if (details is null)
        {
            return NotFound(email);
        }

        var user = details.User;
        Console.WriteLine($"Email: {user.Email}");
        Console.WriteLine($"Display name: {user.DisplayName}");
        Console.WriteLine($"Active: {YesNo(user.IsActive)}");
        Console.WriteLine($"Email confirmed: {YesNo(user.EmailConfirmedAt.HasValue)}");
        Console.WriteLine($"Created: {user.CreatedAt:u}");
        Console.WriteLine($"Last login: {FormatDate(user.LastLoginAt)}");
        Console.WriteLine($"Owned boards: {user.OwnedBoardCount}");
        Console.WriteLine($"Workspace memberships: {user.MembershipCount}");
        Console.WriteLine();
        Console.WriteLine("Sessions:");

        foreach (var session in details.Sessions)
        {
            var state = session.RevokedAt.HasValue
                ? $"revoked {session.RevokedAt:u}"
                : session.ExpiresAt <= DateTimeOffset.UtcNow
                    ? "expired"
                    : "active";
            Console.WriteLine(
                $"  {session.Id}  {session.SessionType,-12}  {state,-28}  " +
                $"last seen {session.LastSeenAt:u}  {session.DeviceName ?? "(unnamed)"}");
        }

        return 0;
    }

    private async Task<int> LockUserAsync(string[] args, CancellationToken cancellationToken)
    {
        var email = RequirePositional(args, 2, "email");
        var actor = RequireOperator(args);
        var reason = RequireOption(args, "--reason");
        return await _service.LockUserAsync(email, actor, reason, cancellationToken)
            ? Success($"Locked {email} and revoked its active sessions.")
            : NotFound(email);
    }

    private async Task<int> UnlockUserAsync(string[] args, CancellationToken cancellationToken)
    {
        var email = RequirePositional(args, 2, "email");
        var actor = RequireOperator(args);
        var reason = RequireOption(args, "--reason");
        return await _service.UnlockUserAsync(email, actor, reason, cancellationToken)
            ? Success($"Unlocked {email}.")
            : NotFound(email);
    }

    private async Task<int> RevokeSessionsAsync(string[] args, CancellationToken cancellationToken)
    {
        var email = RequirePositional(args, 2, "email");
        var actor = RequireOperator(args);
        var reason = RequireOption(args, "--reason");
        var count = await _service.RevokeSessionsAsync(email, actor, reason, cancellationToken);
        return count.HasValue
            ? Success($"Revoked {count.Value} active session(s) for {email}.")
            : NotFound(email);
    }

    private async Task<int> DeleteUserAsync(string[] args, CancellationToken cancellationToken)
    {
        var email = RequirePositional(args, 2, "email");
        var actor = RequireOperator(args);
        var reason = RequireOption(args, "--reason");
        var confirmationEmail = RequireOption(args, "--confirm");
        var result = await _service.DeleteUserAsync(
            email,
            confirmationEmail,
            actor,
            reason,
            cancellationToken);

        if (result is null)
        {
            return NotFound(email);
        }

        Console.WriteLine($"Deleted account {result.Email}.");
        Console.WriteLine($"Boards deleted: {result.DeletedBoardCount}");
        Console.WriteLine($"Sessions deleted: {result.DeletedSessionCount}");
        Console.WriteLine($"Shares deleted: {result.DeletedShareCount}");
        Console.WriteLine($"Unused templates deleted: {result.DeletedTemplateCount}");
        Console.WriteLine($"Referenced templates preserved without an owner: {result.PreservedTemplateCount}");
        return 0;
    }

    private static string RequireOperator(string[] args)
    {
        var actor = GetOption(args, "--actor") ??
            Environment.GetEnvironmentVariable("DUMPTETHER_OPERATOR_NAME");

        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new ArgumentException(
                "A named operator is required. Pass --actor or set DUMPTETHER_OPERATOR_NAME.");
        }

        return actor.Trim();
    }

    private static string RequireOption(string[] args, string name) =>
        GetOption(args, name) ??
        throw new ArgumentException($"Missing required option {name}.");

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.FindIndex(args, value =>
            string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : null;
    }

    private static string RequirePositional(string[] args, int index, string name) =>
        args.ElementAtOrDefault(index) ??
        throw new ArgumentException($"Missing required {name}.");

    private static bool IsHelp(string value) => value is "help" or "-h" or "--help";

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static string FormatDate(DateTimeOffset? value) => value?.ToString("u") ?? "never";

    private static int Success(string message)
    {
        Console.WriteLine(message);
        return 0;
    }

    private static int NotFound(string email)
    {
        Console.Error.WriteLine($"No user found for {email}.");
        return 4;
    }

    private static int UnknownCommand()
    {
        Console.Error.WriteLine("Unknown command.");
        ShowHelp();
        return 2;
    }

    internal static void ShowHelp()
    {
        Console.WriteLine("DumpTether server administration");
        Console.WriteLine();
        Console.WriteLine("Read-only commands:");
        Console.WriteLine("  users list [--search text] [--limit 100]");
        Console.WriteLine("  users show <email>");
        Console.WriteLine();
        Console.WriteLine("Mutating commands require --actor and --reason:");
        Console.WriteLine("  users lock <email> --actor <name> --reason <text>");
        Console.WriteLine("  users unlock <email> --actor <name> --reason <text>");
        Console.WriteLine("  sessions revoke <email> --actor <name> --reason <text>");
        Console.WriteLine("  users delete <email> --confirm <email> --actor <name> --reason <text>");
        Console.WriteLine();
        Console.WriteLine("This tool is intended for SSH-only server operation. It never prints password or token hashes.");
    }
}
