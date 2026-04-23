using Microsoft.Extensions.DependencyInjection;
using TradingIntel.Application.FutGg;
using TradingIntel.Application.Futbin;
using TradingIntel.Application.Snapshots;
using TradingIntel.Infrastructure.FutGg;
using TradingIntel.Infrastructure.Futbin;
using TradingIntel.Infrastructure.Snapshots;

namespace TradingIntel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IRawSnapshotStore>(sp => new FileRawSnapshotStore(AppContext.BaseDirectory));

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
