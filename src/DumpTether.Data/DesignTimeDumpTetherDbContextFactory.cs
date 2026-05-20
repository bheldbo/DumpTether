using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DumpTether.Data;

public sealed class DesignTimeDumpTetherDbContextFactory : IDesignTimeDbContextFactory<DumpTetherDbContext>
{
    private const string LocalDevelopmentConnectionString =
        "Host=localhost;Port=5432;Database=dumptether;Username=dumptether;Password=dumptether_dev_password";

    public DumpTetherDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DumpTether") ??
            LocalDevelopmentConnectionString;

        var options = new DbContextOptionsBuilder<DumpTetherDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new DumpTetherDbContext(options);
    }
}
