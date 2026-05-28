using DumpTether.App;
using DumpTether.Api;
using DumpTether.Data;
using DumpTether.App.Workspaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDumpTetherApplication();
builder.Services.AddDumpTetherData(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentWorkspaceSelection, CurrentWorkspaceSelection>();
builder.Services.AddControllers();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "DumpTether.Api"
}));

app.MapControllers();

app.Run();

public partial class Program;
