using TradingIntel.Application;
using TradingIntel.Infrastructure;
using TradingIntel.Infrastructure.Persistence;
using TradingIntel.Worker;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddApplication();
        services.AddInfrastructure(context.Configuration);
        services.AddCollectionJobs(context.Configuration);
    })
    .Build();

host.Services.MigrateTradingIntelDatabase();

host.Run();
