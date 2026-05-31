using Microsoft.Extensions.DependencyInjection;
using DumpTether.App.Auth;
using DumpTether.App.ArchiveResolutions;
using DumpTether.App.Projects;
using DumpTether.App.Tasks;
using DumpTether.App.Templates;
using DumpTether.App.Views;
using DumpTether.App.Workspaces;

namespace DumpTether.App;

public static class DependencyInjection
{
    public static IServiceCollection AddDumpTetherApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHashService, PasswordHashService>();
        services.AddSingleton<ISessionTokenService, SessionTokenService>();
        services.AddScoped<IArchiveResolutionService, ArchiveResolutionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICurrentUserSessionProvider, CurrentUserSessionProvider>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITaskItemService, TaskItemService>();
        services.AddScoped<ITaskTemplateService, TaskTemplateService>();
        services.AddScoped<ISavedViewService, SavedViewService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();

        return services;
    }
}
