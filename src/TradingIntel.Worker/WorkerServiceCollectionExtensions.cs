using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TradingIntel.Worker.Health;
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
        services.AddSingleton<IJobHealthRegistry, InMemoryJobHealthRegistry>();

        services
            .AddOptions<SbcCollectionOptions>()
            .Bind(configuration.GetSection("Jobs:SbcCollection"));

        services
            .AddOptions<PriceCollectionOptions>()
            .Bind(configuration.GetSection("Jobs:PriceCollection"));

        services.AddHostedService<SbcCollectionJob>();
        services.AddHostedService<PriceCollectionJob>();

        return services;
    }
}
