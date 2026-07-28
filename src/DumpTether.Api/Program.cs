using DumpTether.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var runtimeSetup = DumpTetherRuntimeSetupReader.Read(
    builder.Configuration,
    builder.Environment.IsDevelopment(),
    builder.Environment.IsEnvironment("Desktop"));

builder.Services.AddDumpTetherApiRuntime(
    builder.Configuration,
    builder.Environment,
    runtimeSetup);

var app = builder.Build();

await app.ApplyDumpTetherDatabaseStartupAsync(runtimeSetup);
app.UseDumpTetherApiRuntime(runtimeSetup);

app.Run();

public partial class Program;
