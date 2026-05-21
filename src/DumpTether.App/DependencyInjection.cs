using Microsoft.Extensions.DependencyInjection;
using DumpTether.App.ArchiveResolutions;
using DumpTether.App.Tasks;

namespace DumpTether.App;

public static class DependencyInjection
{
    public static IServiceCollection AddDumpTetherApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IArchiveResolutionService, ArchiveResolutionService>();
        services.AddScoped<ITaskItemService, TaskItemService>();

        return services;
    }
}
