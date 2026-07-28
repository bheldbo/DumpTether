using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DumpTether.Data;

public sealed class DesignTimeDumpTetherDbContextFactory : IDesignTimeDbContextFactory<DumpTetherDbContext>
{
    private const string SqliteMigrationsAssembly = "DumpTether.Data.Sqlite";

    private const string LocalDevelopmentConnectionString =
        "Host=localhost;Port=5432;Database=dumptether;Username=dumptether;Password=dumptether_dev_password";

    public DumpTetherDbContext CreateDbContext(string[] args)
    {
        var provider = DumpTetherDatabaseOptions.NormalizeProvider(
            Environment.GetEnvironmentVariable("Database__Provider") ??
            DumpTetherDatabaseOptions.PostgresProvider);
        var optionsBuilder = new DbContextOptionsBuilder<DumpTetherDbContext>();

        if (DumpTetherDatabaseOptions.IsSqlite(provider))
        {
            var sqliteConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Sqlite:Path"] = Environment.GetEnvironmentVariable(
                        "Database__Sqlite__Path"),
                    ["ConnectionStrings:DumpTether"] = Environment.GetEnvironmentVariable(
                        "ConnectionStrings__DumpTether")
                })
                .Build();

            optionsBuilder.UseSqlite(
                DumpTetherDatabaseOptions.GetSqliteConnectionString(sqliteConfiguration),
                sqliteOptions => sqliteOptions.MigrationsAssembly(SqliteMigrationsAssembly));
        }
        else
        {
            var connectionString =
                Environment.GetEnvironmentVariable("ConnectionStrings__DumpTether") ??
                LocalDevelopmentConnectionString;

            optionsBuilder.UseNpgsql(connectionString);
        }

        return new DumpTetherDbContext(optionsBuilder.Options);
    }
}
