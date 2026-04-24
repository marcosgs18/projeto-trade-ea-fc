using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using TradingIntel.Api;
using TradingIntel.Application;
using TradingIntel.Application.Trading;
using TradingIntel.Infrastructure;
using TradingIntel.Infrastructure.Persistence;

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

if (!app.Environment.IsEnvironment("Testing"))
{
    app.Services.MigrateTradingIntelDatabase();
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
