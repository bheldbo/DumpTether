using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;

namespace DumpTether.Data;

public sealed class DumpTetherDbContext : DbContext
{
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DumpTetherDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
