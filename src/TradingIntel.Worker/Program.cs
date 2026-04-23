using TradingIntel.Application;
using TradingIntel.Infrastructure;
using TradingIntel.Worker;

Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        services.AddApplication();
        services.AddInfrastructure();
        services.AddHostedService<Worker>();
    })
    .Build()
    .Run();
