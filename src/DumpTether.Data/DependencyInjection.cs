using DumpTether.App.ArchiveResolutions;
using DumpTether.App.Auth;
using DumpTether.App.Projects;
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
    public static IServiceCollection AddDumpTetherData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DumpTether");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing connection string 'DumpTether'. Configure ConnectionStrings:DumpTether with a PostgreSQL connection string.");
        }

        services.AddDbContext<DumpTetherDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IArchiveResolutionRepository, EfArchiveResolutionRepository>();
        services.AddScoped<IAuthRepository, EfAuthRepository>();
        services.AddScoped<IDevelopmentWorkspaceProvider, DevelopmentWorkspaceProvider>();
        services.AddScoped<IProjectRepository, EfProjectRepository>();
        services.AddScoped<ISavedViewRepository, EfSavedViewRepository>();
        services.AddScoped<ITaskItemRepository, EfTaskItemRepository>();
        services.AddScoped<ITaskTemplateRepository, EfTaskTemplateRepository>();
        services.AddScoped<IWorkspaceRepository, EfWorkspaceRepository>();

        return services;
    }
}
