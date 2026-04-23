using TradingIntel.Application;
using TradingIntel.Infrastructure;
using TradingIntel.Worker;

Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddApplication();
        services.AddInfrastructure(context.Configuration);
        services.AddCollectionJobs(context.Configuration);
    })
    .Build()
    .Run();
