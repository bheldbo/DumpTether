using System.Text.RegularExpressions;
using DumpTether.Data;
using DumpTether.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
var apiConfigRoot = Path.Combine(repoRoot, "src", "DumpTether.Api");

LoadDotEnv(Path.Combine(repoRoot, ".env"));
ApplyConfigurationAliases();

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
if (string.IsNullOrWhiteSpace(environment))
{
    environment = "Development";
}

EnsureLocalConnectionStringDefault(environment);

var configuration = new ConfigurationBuilder()
    .SetBasePath(apiConfigRoot)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection()
    .AddDumpTetherData(configuration)
    .BuildServiceProvider(validateScopes: true);

var command = args.FirstOrDefault()?.Trim().ToLowerInvariant() ?? "menu";

return command switch
{
    "menu" => await RunMenuAsync(services, configuration),
    "migrate" => await MigrateAsync(services, configuration),
    "status" => await ShowStatusAsync(services, configuration),
    "seed-test-data" => await SeedTestDataAsync(services, configuration),
    "clear-tasks" => await ClearTaskDataAsync(services),
    "reset" => await ResetDatabaseAsync(services),
    "local-info" => ShowLocalInfo(configuration),
    "remove-local-sqlite" => RemoveLocalSqlite(configuration),
    "help" or "-h" or "--help" => ShowHelp(),
    _ => UnknownCommand(command)
};

static async Task<int> RunMenuAsync(IServiceProvider services, IConfiguration configuration)
{
    while (true)
    {
        Console.WriteLine();
        Console.WriteLine("DumpTether database tools");
        Console.WriteLine("1. Status");
        Console.WriteLine("2. Apply EF migrations");
        Console.WriteLine("3. Seed development test data");
        Console.WriteLine("4. Clear task data only");
        Console.WriteLine("5. Reset configured database");
        Console.WriteLine("6. Show local SQLite path");
        Console.WriteLine("7. Delete local SQLite database");
        Console.WriteLine("Q. Quit");
        Console.Write("Choose: ");

        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        switch (choice)
        {
            case "1":
                await ShowStatusAsync(services, configuration);
                break;
            case "2":
                await MigrateAsync(services, configuration);
                break;
            case "3":
                await SeedTestDataAsync(services, configuration);
                break;
            case "4":
                await ClearTaskDataAsync(services);
                break;
            case "5":
                await ResetDatabaseAsync(services);
                break;
            case "6":
                ShowLocalInfo(configuration);
                break;
            case "7":
                RemoveLocalSqlite(configuration);
                break;
            case "Q":
                return 0;
            default:
                Console.WriteLine("Unknown choice.");
                break;
        }
    }
}

static async Task<int> MigrateAsync(IServiceProvider services, IConfiguration configuration)
{
    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();

    Console.WriteLine($"Provider: {DumpTetherDatabaseOptions.GetProvider(configuration)}");
    Console.WriteLine("Applying EF migrations...");
    await db.Database.MigrateAsync();
    Console.WriteLine("Database is up to date.");

    return 0;
}

static async Task<int> ShowStatusAsync(IServiceProvider services, IConfiguration configuration)
{
    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
    var connectionString = db.Database.GetDbConnection().ConnectionString;

    Console.WriteLine($"Environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}");
    Console.WriteLine($"Provider: {DumpTetherDatabaseOptions.GetProvider(configuration)}");
    Console.WriteLine($"Connection: {RedactConnectionString(connectionString)}");
    Console.WriteLine($"Can connect: {await db.Database.CanConnectAsync()}");

    var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();

    Console.WriteLine($"Applied migrations: {applied.Length}");
    Console.WriteLine($"Pending migrations: {pending.Length}");

    foreach (var migration in pending)
    {
        Console.WriteLine($"  - {migration}");
    }

    return 0;
}

static async Task<int> SeedTestDataAsync(IServiceProvider services, IConfiguration configuration)
{
    if (!string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
            "Development",
            StringComparison.OrdinalIgnoreCase))
    {
        RequireTypedConfirmation(
            "This seeds the configured database while not running in Development.",
            "SEED TEST DATA");
    }

    await MigrateAsync(services, configuration);

    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
    var now = DateTimeOffset.UtcNow;

    var email = GetEnvironmentValue("DUMPTETHER_SEED_EMAIL", "seed@dumptether.local");
    var password = GetEnvironmentValue("DUMPTETHER_SEED_PASSWORD", "DumpTether123!");
    var normalizedEmail = AppUser.NormalizeEmail(email);

    var user = await db.AppUsers
        .SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail);

    if (user is null)
    {
        var passwordHasher = new PasswordHasher<object>();
        user = AppUser.Create(
            email,
            "Seed User",
            passwordHasher.HashPassword(new object(), password),
            now,
            emailIsConfirmed: true);
        await db.AppUsers.AddAsync(user);
    }

    var workspace = await db.WorkspaceMemberships
        .Where(candidate => candidate.UserId == user.Id)
        .Join(
            db.Workspaces,
            membership => membership.WorkspaceId,
            candidate => candidate.Id,
            (membership, candidate) => candidate)
        .FirstOrDefaultAsync(candidate => candidate.Name == "Seed Board");

    if (workspace is null)
    {
        workspace = Workspace.Create("Seed Board", now);
        workspace.ChangeColor("#FFD86B", now);
        await db.Workspaces.AddAsync(workspace);
    }

    if (!await db.WorkspaceMemberships.AnyAsync(candidate =>
            candidate.WorkspaceId == workspace.Id &&
            candidate.UserId == user.Id))
    {
        await db.WorkspaceMemberships.AddAsync(
            WorkspaceMembership.Create(
                workspace.Id,
                user.Id,
                WorkspaceMembershipRole.Owner,
                now));
    }

    var generalProject = await EnsureProjectAsync(db, workspace.Id, "General", "#FFE36D", now);
    var followUpProject = await EnsureProjectAsync(db, workspace.Id, "Follow-up", "#A7D8FF", now);
    var basicTemplate = await EnsureBasicTemplateAsync(db, user.Id, now);
    var todoTemplate = await EnsureTodoTemplateAsync(db, user.Id, now);

    await EnsureTaskAsync(
        db,
        workspace.Id,
        generalProject.Id,
        basicTemplate.Id,
        "Seed: capture parking permit note",
        "Permits expire soon. Check the city portal and save the renewal link.",
        "#FFF4A8",
        "General",
        null,
        now.AddDays(2),
        now);
    await EnsureTaskAsync(
        db,
        workspace.Id,
        followUpProject.Id,
        todoTemplate.Id,
        "Seed: test checklist task",
        "Open the task and tick the entry checkbox.",
        "#BFE7D2",
        "Follow-up",
        "Waiting",
        now.AddDays(1),
        now);

    await db.SaveChangesAsync();

    Console.WriteLine("Development seed data is ready.");
    Console.WriteLine($"Seed user: {email}");
    Console.WriteLine($"Seed password: {password}");

    return 0;
}

static async Task<int> ClearTaskDataAsync(IServiceProvider services)
{
    RequireTypedConfirmation(
        "This clears tasks, notes, field values and task shares. Users, boards, categories, templates and settings stay.",
        "CLEAR TASKS");

    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();

    db.TaskTimelineEntryFieldValues.RemoveRange(db.TaskTimelineEntryFieldValues);
    db.TaskTimelineEntries.RemoveRange(db.TaskTimelineEntries);
    db.FieldValues.RemoveRange(db.FieldValues);
    db.TaskItemShares.RemoveRange(db.TaskItemShares);
    db.TaskItems.RemoveRange(db.TaskItems);

    await db.SaveChangesAsync();
    Console.WriteLine("Task data cleared.");

    return 0;
}

static async Task<int> ResetDatabaseAsync(IServiceProvider services)
{
    RequireTypedConfirmation(
        "This deletes and recreates the configured database. Use it only for local development data.",
        "RESET DATABASE");

    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();

    await db.Database.EnsureDeletedAsync();
    await db.Database.MigrateAsync();
    Console.WriteLine("Database reset and migrations applied.");

    return 0;
}

static int ShowLocalInfo(IConfiguration configuration)
{
    var path = DumpTetherDatabaseOptions.GetSqliteDatabasePath(configuration);
    Console.WriteLine($"SQLite path: {path}");
    Console.WriteLine($"Exists: {File.Exists(path)}");

    return 0;
}

static int RemoveLocalSqlite(IConfiguration configuration)
{
    var path = DumpTetherDatabaseOptions.GetSqliteDatabasePath(configuration);

    RequireTypedConfirmation($"This deletes the local SQLite database at {path}.", "DELETE SQLITE");

    if (File.Exists(path))
    {
        File.Delete(path);
        Console.WriteLine($"Deleted {path}");
    }
    else
    {
        Console.WriteLine($"No SQLite database found at {path}");
    }

    return 0;
}

static int ShowHelp()
{
    Console.WriteLine("DumpTether.Database");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  menu                 Interactive menu");
    Console.WriteLine("  status               Show provider, connection and migration status");
    Console.WriteLine("  migrate              Apply EF migrations");
    Console.WriteLine("  seed-test-data       Apply migrations and add reusable development sample data");
    Console.WriteLine("  clear-tasks          Clear task/note data only");
    Console.WriteLine("  reset                Delete and recreate configured database");
    Console.WriteLine("  local-info           Show local SQLite path");
    Console.WriteLine("  remove-local-sqlite  Delete local SQLite file");

    return 0;
}

static async Task<Project> EnsureProjectAsync(
    DumpTetherDbContext db,
    Guid workspaceId,
    string name,
    string color,
    DateTimeOffset now)
{
    var project = await db.Projects
        .FirstOrDefaultAsync(candidate =>
            candidate.WorkspaceId == workspaceId &&
            candidate.Name == name);

    if (project is not null)
    {
        return project;
    }

    project = Project.Create(workspaceId, name, now);
    project.ChangeColor(color);
    await db.Projects.AddAsync(project);

    return project;
}

static async Task<TaskTemplate> EnsureBasicTemplateAsync(
    DumpTetherDbContext db,
    Guid ownerUserId,
    DateTimeOffset now)
{
    var template = await db.TaskTemplates
        .Include("_fieldDefinitions")
        .FirstOrDefaultAsync(candidate =>
            candidate.OwnerUserId == ownerUserId &&
            candidate.Name == "Basic Task" &&
            candidate.DeletedAt == null);

    if (template is not null)
    {
        return template;
    }

    template = TaskTemplate.Create(ownerUserId, "Basic Task", now);
    template.AddFieldDefinition(
        "context",
        "Context",
        FieldDefinitionType.LongText,
        FieldDefinitionScope.Header,
        isRequired: false,
        sortOrder: 0,
        layoutRow: 1,
        layoutColumn: 1,
        layoutWeight: 1);
    template.UpdateLayout(
        """[{"row":1,"columnWeights":[1],"height":168}]""",
        """[{"row":1,"columnWeights":[1],"height":132}]""",
        now);
    await db.TaskTemplates.AddAsync(template);

    return template;
}

static async Task<TaskTemplate> EnsureTodoTemplateAsync(
    DumpTetherDbContext db,
    Guid ownerUserId,
    DateTimeOffset now)
{
    var template = await db.TaskTemplates
        .Include("_fieldDefinitions")
        .FirstOrDefaultAsync(candidate =>
            candidate.OwnerUserId == ownerUserId &&
            candidate.Name == "ToDo Task" &&
            candidate.DeletedAt == null);

    if (template is not null)
    {
        return template;
    }

    template = TaskTemplate.Create(ownerUserId, "ToDo Task", now);
    template.AddFieldDefinition(
        "done",
        "Done?",
        FieldDefinitionType.Checkbox,
        FieldDefinitionScope.Entry,
        isRequired: false,
        sortOrder: 0,
        layoutRow: 1,
        layoutColumn: 1,
        layoutWeight: 0.22);
    template.AddFieldDefinition(
        "step",
        "Step",
        FieldDefinitionType.LongText,
        FieldDefinitionScope.Entry,
        isRequired: false,
        sortOrder: 1,
        layoutRow: 1,
        layoutColumn: 2,
        layoutWeight: 1.78);
    template.UpdateLayout(
        """[{"row":1,"columnWeights":[1],"height":132}]""",
        """[{"row":1,"columnWeights":[0.22,1.78],"height":144}]""",
        now);
    await db.TaskTemplates.AddAsync(template);

    return template;
}

static async Task EnsureTaskAsync(
    DumpTetherDbContext db,
    Guid workspaceId,
    Guid projectId,
    Guid templateId,
    string title,
    string note,
    string color,
    string category,
    string? status,
    DateTimeOffset? followUpAt,
    DateTimeOffset now)
{
    if (await db.TaskItems.AnyAsync(candidate =>
            candidate.WorkspaceId == workspaceId &&
            candidate.Title == title))
    {
        return;
    }

    var taskItem = TaskItem.Create(workspaceId, projectId, title, now, templateId);
    taskItem.ChangeColor(color, now);
    taskItem.ChangeCategory(category, now);
    if (!string.IsNullOrWhiteSpace(status))
    {
        taskItem.ChangeStatus(status, now);
    }

    taskItem.SetFollowUp(followUpAt, now);
    taskItem.AddNote(note, now);
    await db.TaskItems.AddAsync(taskItem);
}

static int UnknownCommand(string command)
{
    Console.WriteLine($"Unknown command '{command}'.");
    ShowHelp();

    return 2;
}

static void RequireTypedConfirmation(string message, string requiredText)
{
    Console.WriteLine();
    Console.WriteLine(message);
    Console.Write($"Type '{requiredText}' to continue: ");

    var typed = Console.ReadLine();
    if (!string.Equals(typed, requiredText, StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Cancelled.");
    }
}

static string RedactConnectionString(string connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return "(empty)";
    }

    return Regex.Replace(
        connectionString,
        "(Password|Pwd)\\s*=\\s*[^;]+",
        "$1=<redacted>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}

static string FindRepoRoot(string startDirectory)
{
    var directory = new DirectoryInfo(startDirectory);

    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "DumpTether.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not locate DumpTether.sln from the database runner path.");
}

static void LoadDotEnv(string path)
{
    if (!File.Exists(path))
    {
        return;
    }

    foreach (var rawLine in File.ReadAllLines(path))
    {
        var line = rawLine.Trim();

        if (line.Length == 0 || line.StartsWith('#'))
        {
            continue;
        }

        var equalsIndex = line.IndexOf('=');
        if (equalsIndex <= 0)
        {
            continue;
        }

        var name = line[..equalsIndex].Trim();
        var value = RemoveInlineDotEnvComment(line[(equalsIndex + 1)..].Trim()).Trim('"', '\'');

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}

static string RemoveInlineDotEnvComment(string value)
{
    var quote = '\0';

    for (var index = 0; index < value.Length; index++)
    {
        var character = value[index];

        if (quote != '\0')
        {
            if (character == quote)
            {
                quote = '\0';
            }

            continue;
        }

        if (character is '"' or '\'')
        {
            quote = character;
            continue;
        }

        if (character == '#' && (index == 0 || char.IsWhiteSpace(value[index - 1])))
        {
            return value[..index].TrimEnd();
        }
    }

    return value;
}

static void ApplyConfigurationAliases()
{
    var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["DUMPTETHER_APPLY_MIGRATIONS_ON_STARTUP"] = "Database__ApplyMigrationsOnStartup",
        ["DUMPTETHER_DATABASE_PROVIDER"] = "Database__Provider",
        ["DUMPTETHER_SQLITE_PATH"] = "Database__Sqlite__Path",
        ["DUMPTETHER_AUTH_SESSION_DAYS"] = "Auth__SessionDays",
        ["DUMPTETHER_AUTH_SESSION_CLEANUP_DAYS"] = "Auth__SessionCleanupDays",
        ["DUMPTETHER_AUTH_SESSION_CLEANUP_INTERVAL_HOURS"] = "Auth__SessionCleanupIntervalHours",
        ["DUMPTETHER_ARCHIVE_RETENTION_DAYS"] = "Archive__RetentionDays"
    };

    foreach (var (source, target) in aliases)
    {
        var value = Environment.GetEnvironmentVariable(source);

        if (!string.IsNullOrWhiteSpace(value) &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(target)))
        {
            Environment.SetEnvironmentVariable(target, value);
        }
    }
}

static void EnsureLocalConnectionStringDefault(string environment)
{
    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DumpTether")))
    {
        return;
    }

    var provider = Environment.GetEnvironmentVariable("Database__Provider") ??
        DumpTetherDatabaseOptions.PostgresProvider;

    if (DumpTetherDatabaseOptions.IsSqlite(provider))
    {
        return;
    }

    if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "ConnectionStrings__DumpTether is required when running DumpTether.Database outside Development.");
    }

    var host = GetEnvironmentValue("POSTGRES_HOST", "localhost");
    var port = GetEnvironmentValue("POSTGRES_PORT", "5432");
    var database = GetEnvironmentValue("POSTGRES_DB", "dumptether");
    var username = GetEnvironmentValue("POSTGRES_USER", "dumptether");
    var password = GetEnvironmentValue("POSTGRES_PASSWORD", "dumptether_dev_password");

    Environment.SetEnvironmentVariable(
        "ConnectionStrings__DumpTether",
        $"Host={host};Port={port};Database={database};Username={username};Password={password}");
}

static string GetEnvironmentValue(string name, string fallback) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : fallback;
