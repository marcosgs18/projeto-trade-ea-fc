using Microsoft.Extensions.DependencyInjection;
using TradingIntel.Application.FutGg;
using TradingIntel.Application.Snapshots;
using TradingIntel.Infrastructure.FutGg;
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

        return services;
    }
}
