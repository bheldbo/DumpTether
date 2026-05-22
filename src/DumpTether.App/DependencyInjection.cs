using Microsoft.Extensions.DependencyInjection;
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
        services.AddScoped<IArchiveResolutionService, ArchiveResolutionService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITaskItemService, TaskItemService>();
        services.AddScoped<ITaskTemplateService, TaskTemplateService>();
        services.AddScoped<ISavedViewService, SavedViewService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();

        return services;
    }
}
