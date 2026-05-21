using DumpTether.App;
using DumpTether.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDumpTetherApplication();
builder.Services.AddDumpTetherData(builder.Configuration);
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
