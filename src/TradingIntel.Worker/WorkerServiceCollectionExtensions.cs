using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TradingIntel.Application.Trading;
using TradingIntel.Application.Watchlist;
using TradingIntel.Worker.Jobs;

namespace TradingIntel.Worker;

public static class WorkerServiceCollectionExtensions
{
    /// <summary>
    /// Registers the collection worker's hosted services, health registry and
    /// bound job options. Infrastructure clients and repositories must already
    /// be registered (see <c>AddApplication()</c> and <c>AddInfrastructure()</c>).
    /// </summary>
    public static IServiceCollection AddCollectionJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<SbcCollectionOptions>()
            .Bind(configuration.GetSection("Jobs:SbcCollection"));

        services
            .AddOptions<PriceCollectionOptions>()
            .Bind(configuration.GetSection("Jobs:PriceCollection"));

        services.Configure<OpportunityRecomputeStaleSettings>(
            configuration.GetSection("Jobs:OpportunityRecompute"));

        services
            .AddOptions<OpportunityRecomputeOptions>()
            .Bind(configuration.GetSection("Jobs:OpportunityRecompute"));

        services
            .AddOptions<WatchlistSeedOptions>()
            .Bind(configuration.GetSection(WatchlistSeedOptions.SectionName));

        services.AddHostedService<SbcCollectionJob>();
        services.AddHostedService<PriceCollectionJob>();
        services.AddHostedService<OpportunityRecomputeJob>();

        return services;
    }
}
