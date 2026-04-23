using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradingIntel.Application.FutGg;
using TradingIntel.Application.Futbin;
using TradingIntel.Application.Persistence;
using TradingIntel.Application.Snapshots;
using TradingIntel.Infrastructure.FutGg;
using TradingIntel.Infrastructure.Futbin;
using TradingIntel.Infrastructure.Persistence;
using TradingIntel.Infrastructure.Persistence.Repositories;

namespace TradingIntel.Infrastructure;

public static class DependencyInjection
{
    private const string DefaultConnectionString = "Data Source=tradingintel.db";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var connectionString = configuration?.GetConnectionString("TradingIntel") ?? DefaultConnectionString;

        services.AddDbContext<TradingIntelDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<SqliteRawSnapshotStore>();
        services.AddScoped<IRawSnapshotStore>(sp => sp.GetRequiredService<SqliteRawSnapshotStore>());
        services.AddScoped<IRawSnapshotRepository>(sp => sp.GetRequiredService<SqliteRawSnapshotStore>());
        services.AddScoped<IPlayerPriceSnapshotRepository, PlayerPriceSnapshotRepository>();
        services.AddScoped<IMarketListingSnapshotRepository, MarketListingSnapshotRepository>();
        services.AddScoped<ISbcChallengeRepository, SbcChallengeRepository>();

        services.AddHttpClient<IFutGgSbcClient, FutGgSbcClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<IFutbinMarketClient, FutbinMarketClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TradingIntel/1.0 (+https://github.com/marcosgs18/projeto-trade-ea-fc)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        return services;
    }
}
