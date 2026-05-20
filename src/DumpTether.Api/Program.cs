using DumpTether.App;
using DumpTether.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDumpTetherApplication();
builder.Services.AddDumpTetherData(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "DumpTether.Api"
}));

app.Run();
