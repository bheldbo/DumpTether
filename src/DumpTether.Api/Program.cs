using DumpTether.Api;

var builder = WebApplication.CreateBuilder(args);
var runtimeSetup = DumpTetherRuntimeSetupReader.Read(
    builder.Configuration,
    builder.Environment.IsDevelopment());

builder.Services.AddDumpTetherApiRuntime(
    builder.Configuration,
    builder.Environment,
    runtimeSetup);

var app = builder.Build();

await app.ApplyDumpTetherDatabaseStartupAsync(runtimeSetup);
app.UseDumpTetherApiRuntime(runtimeSetup);

app.Run();

public partial class Program;
