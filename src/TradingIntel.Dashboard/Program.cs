using TradingIntel.Application;
using TradingIntel.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

var app = builder.Build();

app.MapGet("/", () => Results.Text("TradingIntel.Dashboard", "text/plain"));

app.Run();
