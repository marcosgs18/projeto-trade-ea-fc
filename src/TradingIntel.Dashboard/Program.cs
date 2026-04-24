using TradingIntel.Application;
using TradingIntel.Application.Trading;
using TradingIntel.Application.Watchlist;
using TradingIntel.Dashboard.Components;
using TradingIntel.Infrastructure;
using TradingIntel.Infrastructure.Persistence;
using TradingIntel.Infrastructure.Watchlist;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<OpportunityRecomputeStaleSettings>(
    builder.Configuration.GetSection("Jobs:OpportunityRecompute"));
builder.Services
    .AddOptions<WatchlistSeedOptions>()
    .Bind(builder.Configuration.GetSection(WatchlistSeedOptions.SectionName));

var app = builder.Build();

// In the Testing environment the host is created by WebApplicationFactory
// which is responsible for migrating the in-memory SQLite and seeding the
// watchlist after the host starts (mirrors the Api's composition root).
if (!app.Environment.IsEnvironment("Testing"))
{
    app.Services.MigrateTradingIntelDatabase();
    await app.Services
        .SeedWatchlistAsync(app.Configuration)
        .ConfigureAwait(false);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Namespaced so WebApplicationFactory<TradingIntel.Dashboard.Program> disambiguates
// this entry point from TradingIntel.Api's global `Program`.
namespace TradingIntel.Dashboard
{
    public partial class Program;
}
