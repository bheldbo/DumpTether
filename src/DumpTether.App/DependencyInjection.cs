using Microsoft.Extensions.DependencyInjection;

namespace DumpTether.App;

public static class DependencyInjection
{
    public static IServiceCollection AddDumpTetherApplication(this IServiceCollection services)
    {
        return services;
    }
}
