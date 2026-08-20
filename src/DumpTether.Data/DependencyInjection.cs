using DumpTether.App.Administration;
using DumpTether.App.ArchiveResolutions;
using DumpTether.App.Auth;
using DumpTether.App.Projects;
using DumpTether.App.Notifications;
using DumpTether.App.Sync;
using DumpTether.App.Tasks;
using DumpTether.App.Templates;
using DumpTether.App.Views;
using DumpTether.App.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DumpTether.Data;

public static class DependencyInjection
{
    private const string SqliteMigrationsAssembly = "DumpTether.Data.Sqlite";

    public static IServiceCollection AddDumpTetherData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = DumpTetherDatabaseOptions.GetProvider(configuration);

        if (DumpTetherDatabaseOptions.IsSqlite(provider))
        {
            var sqliteConnectionString = DumpTetherDatabaseOptions.GetSqliteConnectionString(configuration);
            services.AddDbContext<DumpTetherDbContext>(options =>
                options.UseSqlite(
                    sqliteConnectionString,
                    sqliteOptions => sqliteOptions.MigrationsAssembly(SqliteMigrationsAssembly)));
        }
        else
        {
            var connectionString = configuration.GetConnectionString("DumpTether");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Missing connection string 'DumpTether'. Configure ConnectionStrings:DumpTether with a PostgreSQL connection string, " +
                    "or set Database:Provider to Sqlite for a local offline database.");
            }

            services.AddDbContext<DumpTetherDbContext>(options =>
                options.UseNpgsql(connectionString));
        }

        services.AddScoped<IArchiveResolutionRepository, EfArchiveResolutionRepository>();
        services.AddScoped<IAdministrationRepository, EfAdministrationRepository>();
        services.AddScoped<IAuthRepository, EfAuthRepository>();
        services.AddScoped<IUserNotificationRepository, EfUserNotificationRepository>();
        services.AddScoped<IRegistrationTransaction, EfRegistrationTransaction>();
        services.AddScoped<IDevelopmentWorkspaceProvider, DevelopmentWorkspaceProvider>();
        services.AddScoped<IProjectRepository, EfProjectRepository>();
        services.AddScoped<ISavedViewRepository, EfSavedViewRepository>();
        services.AddScoped<ISyncRepository, EfSyncRepository>();
        services.AddScoped<ITaskItemRepository, EfTaskItemRepository>();
        services.AddScoped<ITaskTemplateRepository, EfTaskTemplateRepository>();
        services.AddScoped<IWorkspaceRepository, EfWorkspaceRepository>();

        return services;
    }
}
