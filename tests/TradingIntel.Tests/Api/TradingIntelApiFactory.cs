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

namespace TradingIntel.Tests.Api;

/// <summary>
/// API host com SQLite in-memory (mesmo padrão de <see cref="Infrastructure.Persistence.PersistenceTestFixture"/>).
/// </summary>
public class TradingIntelApiFactory : WebApplicationFactory<Program>
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
                    ["Jobs:OpportunityRecompute:Players:0:PlayerId"] = "90001",
                    ["Jobs:OpportunityRecompute:Players:0:Name"] = "Api Player",
                    ["Jobs:OpportunityRecompute:Players:0:Overall"] = "84",
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

        // Production Program.cs skips seeding when IsEnvironment("Testing"),
        // leaving it to the factory so the in-memory SQLite is migrated first.
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
