using DumpTether.App.Administration;
using DumpTether.App.ArchiveResolutions;
using DumpTether.App.Auth;
using DumpTether.App.Email;
using DumpTether.App.LiveUpdates;
using DumpTether.App.Projects;
using DumpTether.App.Sync;
using DumpTether.App.Tasks;
using DumpTether.App.Templates;
using DumpTether.App.Views;
using DumpTether.App.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;

namespace DumpTether.App;

public static class DependencyInjection
{
    public static IServiceCollection AddDumpTetherApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHashService, PasswordHashService>();
        services.AddSingleton<ISessionTokenService, SessionTokenService>();
        services.AddSingleton<IEmailSender, NoOpEmailSender>();
        services.AddSingleton<ILiveUpdatePublisher, NoOpLiveUpdatePublisher>();
        services.AddSingleton<ICloudSyncClient, NoOpCloudSyncClient>();
        services.AddSingleton<ICloudSessionProtector, NoOpCloudSessionProtector>();
        services.AddScoped<IAdministrationService, AdministrationService>();
        services.AddScoped<IArchiveResolutionService, ArchiveResolutionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICurrentUserSessionProvider, CurrentUserSessionProvider>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<ITaskItemService, TaskItemService>();
        services.AddScoped<IBuiltInTaskTemplateProvisioner, BuiltInTaskTemplateProvisioner>();
        services.AddScoped<ITaskTemplateService, TaskTemplateService>();
        services.AddScoped<ISavedViewService, SavedViewService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();

        return services;
    }

    public static IServiceCollection AddDumpTetherTransactionalEmail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EmailOptions>(configuration.GetSection("Email"));
        var options = configuration.GetSection("Email").Get<EmailOptions>() ?? new();
        if (options.Provider == EmailProvider.None)
        {
            return services;
        }

        services.RemoveAll<IEmailSender>();
        if (options.Provider == EmailProvider.Smtp)
        {
            services.AddTransient<IEmailSender, SmtpTransactionalEmailSender>();
            return services;
        }

        if (options.Provider == EmailProvider.BrevoApi)
        {
            services.AddHttpClient<IEmailSender, BrevoApiEmailSender>();
            return services;
        }

        throw new InvalidOperationException($"Unsupported email provider '{options.Provider}'.");
    }
}
