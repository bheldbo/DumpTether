using DumpTether.Admin;
using DumpTether.App;
using DumpTether.App.Administration;
using DumpTether.Data;
using Microsoft.Extensions.DependencyInjection;

if (args.Length == 0 || args[0] is "help" or "-h" or "--help")
{
    AdminCommandRunner.ShowHelp();
    return 0;
}

var configuration = AdminConfiguration.Build();
var services = new ServiceCollection()
    .AddDumpTetherApplication()
    .AddDumpTetherData(configuration)
    .BuildServiceProvider(validateScopes: true);

await using var scope = services.CreateAsyncScope();
var runner = new AdminCommandRunner(
    scope.ServiceProvider.GetRequiredService<IAdministrationService>());

return await runner.RunAsync(args, CancellationToken.None);
