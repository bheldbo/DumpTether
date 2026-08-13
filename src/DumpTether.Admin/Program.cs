using DumpTether.Admin;
using DumpTether.App;
using DumpTether.App.Administration;
using DumpTether.App.Auth;
using DumpTether.Data;
using Microsoft.Extensions.DependencyInjection;

if (args.Length == 0 || args[0] is "help" or "-h" or "--help")
{
    AdminCommandRunner.ShowHelp();
    return 0;
}

var configuration = AdminConfiguration.Build();
var serviceCollection = new ServiceCollection();
serviceCollection.Configure<PasswordRecoveryOptions>(configuration.GetSection("PasswordRecovery"));
var services = serviceCollection
    .AddDumpTetherApplication()
    .AddDumpTetherTransactionalEmail(configuration)
    .AddDumpTetherData(configuration)
    .BuildServiceProvider(validateScopes: true);

await using var scope = services.CreateAsyncScope();
var runner = new AdminCommandRunner(
    scope.ServiceProvider.GetRequiredService<IAdministrationService>(),
    scope.ServiceProvider.GetRequiredService<DumpTether.App.Auth.IAuthService>());

return await runner.RunAsync(args, CancellationToken.None);
