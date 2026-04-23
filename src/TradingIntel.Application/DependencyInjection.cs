using Microsoft.Extensions.DependencyInjection;
using TradingIntel.Application.JobHealth;
using TradingIntel.Application.Sbc;
using TradingIntel.Application.Trading;

namespace TradingIntel.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IJobHealthRegistry, InMemoryJobHealthRegistry>();

        services.AddSingleton(RatingBandDemandWeights.Default);
        services.AddScoped<IRatingBandDemandService, RatingBandDemandService>();

        services.AddSingleton(TradeScoringWeights.Default);
        services.AddScoped<ITradeScoringService, TradeScoringService>();

        services.AddScoped<IOpportunityRecomputeService, OpportunityRecomputeService>();

        return services;
    }
}
