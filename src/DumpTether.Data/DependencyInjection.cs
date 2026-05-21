using DumpTether.App.Tasks;
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

        services.AddScoped<IDevelopmentWorkspaceProvider, DevelopmentWorkspaceProvider>();
        services.AddScoped<ITaskItemRepository, EfTaskItemRepository>();

        return services;
    }
}
