using TradingIntel.Application;
using TradingIntel.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Text("TradingIntel.Api", "text/plain"));

app.Run();

public partial class Program { }
