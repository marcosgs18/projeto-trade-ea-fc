using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using TradingIntel.Api;
using TradingIntel.Application;
using TradingIntel.Application.Trading;
using TradingIntel.Application.Watchlist;
using TradingIntel.Infrastructure;
using TradingIntel.Infrastructure.Persistence;
using TradingIntel.Infrastructure.Watchlist;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<OpportunityRecomputeStaleSettings>(
    builder.Configuration.GetSection("Jobs:OpportunityRecompute"));
builder.Services.Configure<OpportunityRecomputePlayersSource>(
    builder.Configuration.GetSection("Jobs:OpportunityRecompute"));
builder.Services
    .AddOptions<WatchlistSeedOptions>()
    .Bind(builder.Configuration.GetSection(WatchlistSeedOptions.SectionName));
builder.Services.AddHealthChecks();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TradingIntel API",
        Version = "v1",
    });
});

var app = builder.Build();

// In the Testing environment the WebApplicationFactory provisions an
// in-memory SQLite connection and is responsible for running both the
// migration and the watchlist seed after the host starts.
if (!app.Environment.IsEnvironment("Testing"))
{
    app.Services.MigrateTradingIntelDatabase();
    await app.Services
        .SeedWatchlistAsync(app.Configuration)
        .ConfigureAwait(false);
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Text("TradingIntel.Api", "text/plain"));

TradingIntelApiEndpoints.Map(app);

app.Run();

public partial class Program { }
