using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TradingIntel.Infrastructure.Persistence;
using TradingIntel.Infrastructure.Watchlist;

namespace TradingIntel.Tests.Dashboard;

/// <summary>
/// Blazor Server host for the Dashboard backed by an in-memory SQLite. Mirrors
/// <c>TradingIntelApiFactory</c>: the test connection is kept alive by the
/// factory, and migrations + watchlist seed run inside <see cref="CreateHost"/>
/// because the production <c>Program.cs</c> skips both when
/// <c>IsEnvironment("Testing")</c>.
/// </summary>
public class DashboardHostFactory : WebApplicationFactory<TradingIntel.Dashboard.Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:TradingIntel"] = "Data Source=ignored;Mode=Memory",
                    ["Jobs:OpportunityRecompute:StaleAfter"] = "00:15:00",
                });
        });

        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services
                .Where(d => d.ServiceType == typeof(DbContextOptions<TradingIntelDbContext>)
                    || d.ServiceType == typeof(TradingIntelDbContext))
                .ToList())
            {
                services.Remove(d);
            }

            services.AddSingleton(_connection);
            services.AddDbContext<TradingIntelDbContext>(o => o.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using (var scope = host.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<TradingIntelDbContext>().Database.Migrate();
        }

        host.Services
            .SeedWatchlistAsync(host.Services.GetRequiredService<IConfiguration>())
            .GetAwaiter()
            .GetResult();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Dispose();
            _connection = null;
        }

        base.Dispose(disposing);
    }
}
