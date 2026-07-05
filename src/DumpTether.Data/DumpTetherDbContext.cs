using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DumpTether.Data;

public sealed class DumpTetherDbContext : DbContext
{
    private const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";
    private const string PostgreSqlJsonColumnType = "jsonb";
    private const string SqliteJsonColumnType = "TEXT";

    public DumpTetherDbContext(DbContextOptions<DumpTetherDbContext> options)
        : base(options)
    {
    }

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<WorkspaceMembership> WorkspaceMemberships => Set<WorkspaceMembership>();

    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();

    public DbSet<EmailConfirmationToken> EmailConfirmationTokens => Set<EmailConfirmationToken>();

    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<TaskItem> TaskItems => Set<TaskItem>();

    public DbSet<TaskItemShare> TaskItemShares => Set<TaskItemShare>();

    public DbSet<TaskTemplate> TaskTemplates => Set<TaskTemplate>();

    public DbSet<FieldDefinition> FieldDefinitions => Set<FieldDefinition>();

    public DbSet<FieldValue> FieldValues => Set<FieldValue>();

    public DbSet<TaskTimelineEntry> TaskTimelineEntries => Set<TaskTimelineEntry>();

    public DbSet<TaskTimelineEntryFieldValue> TaskTimelineEntryFieldValues =>
        Set<TaskTimelineEntryFieldValue>();

    public DbSet<ArchiveResolution> ArchiveResolutions => Set<ArchiveResolution>();

    public DbSet<SavedView> SavedViews => Set<SavedView>();

    public DbSet<SyncRoot> SyncRoots => Set<SyncRoot>();

    public DbSet<SyncMapping> SyncMappings => Set<SyncMapping>();

    public DbSet<CloudSyncAccount> CloudSyncAccounts => Set<CloudSyncAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DumpTetherDbContext).Assembly);
        ApplyProviderSpecificColumnTypes(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    private void ApplyProviderSpecificColumnTypes(ModelBuilder modelBuilder)
    {
        if (!string.Equals(Database.ProviderName, SqliteProviderName, StringComparison.Ordinal))
        {
            return;
        }

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entityType => entityType.GetProperties()))
        {
            if (string.Equals(property.GetColumnType(), PostgreSqlJsonColumnType, StringComparison.OrdinalIgnoreCase))
            {
                property.SetColumnType(SqliteJsonColumnType);
            }
        }
    }
}
