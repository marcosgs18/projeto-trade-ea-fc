using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradingIntel.Application.FutGg;
using TradingIntel.Application.Persistence;
using TradingIntel.Application.PlayerMarket;
using TradingIntel.Application.Snapshots;
using TradingIntel.Infrastructure.FutGg;
using TradingIntel.Infrastructure.Futbin;
using TradingIntel.Infrastructure.Persistence;
using TradingIntel.Infrastructure.Persistence.Repositories;

namespace TradingIntel.Infrastructure;

public static class DependencyInjection
{
    private const string DefaultConnectionString = "Data Source=data/tradingintel.db";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var rawConnectionString = configuration?.GetConnectionString("TradingIntel") ?? DefaultConnectionString;
        var connectionString = ResolveSqliteConnectionString(rawConnectionString);

        services.AddDbContext<TradingIntelDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<SqliteRawSnapshotStore>();
        services.AddScoped<IRawSnapshotStore>(sp => sp.GetRequiredService<SqliteRawSnapshotStore>());
        services.AddScoped<IRawSnapshotRepository>(sp => sp.GetRequiredService<SqliteRawSnapshotStore>());
        services.AddScoped<IPlayerPriceSnapshotRepository, PlayerPriceSnapshotRepository>();
        services.AddScoped<IMarketListingSnapshotRepository, MarketListingSnapshotRepository>();
        services.AddScoped<ISbcChallengeRepository, SbcChallengeRepository>();
        services.AddScoped<ITradeOpportunityRepository, TradeOpportunityRepository>();

        services.AddHttpClient<IFutGgSbcClient, FutGgSbcClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        RegisterMarketClient(services, configuration);

        return services;
    }

    /// <summary>
    /// Registers the active <see cref="IPlayerMarketClient"/> based on
    /// <c>Market:Source</c>. FUT.GG is the default because its JSON API is
    /// reachable without Cloudflare; FUTBIN is kept as an alternative and can
    /// be re-enabled once a WAF-authorized proxy is wired up.
    /// </summary>
    private static void RegisterMarketClient(IServiceCollection services, IConfiguration? configuration)
    {
        var source = configuration?["Market:Source"]?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(source))
        {
            source = "futgg";
        }

        var resolvedSource = source;
        services.AddOptions<MarketSourceOptions>()
            .Configure(options => options.Source = resolvedSource);

        services.AddOptions<FutGgApiOptions>()
            .Configure(options =>
            {
                configuration?.GetSection(FutGgApiOptions.SectionName).Bind(options);
                if (options.Platforms is null || options.Platforms.Count == 0)
                {
                    options.Platforms = new List<string> { "pc" };
                }
            });

        if (string.Equals(source, "futbin", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IPlayerMarketClient, FutbinMarketClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TradingIntel/1.0 (+https://github.com/marcosgs18/projeto-trade-ea-fc)");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            });
            return;
        }

        services.AddHttpClient<IPlayerMarketClient, FutGgMarketClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
    }

    /// <summary>
    /// Resolves a relative <c>Data Source</c> against the repo root (folder containing
    /// <c>TradingIntel.sln</c>) so Worker and API share the same SQLite file without
    /// depending on each project's <c>bin/</c> working directory. Absolute paths and
    /// in-memory data sources are passed through unchanged.
    /// </summary>
    private static string ResolveSqliteConnectionString(string raw)
    {
        var builder = new SqliteConnectionStringBuilder(raw);
        var dataSource = builder.DataSource;

        if (string.IsNullOrWhiteSpace(dataSource)
            || dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
            || builder.Mode == SqliteOpenMode.Memory
            || Path.IsPathRooted(dataSource))
        {
            return raw;
        }

        var root = FindRepoRoot() ?? AppContext.BaseDirectory;
        var absolute = Path.GetFullPath(Path.Combine(root, dataSource));
        var directory = Path.GetDirectoryName(absolute);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        builder.DataSource = absolute;
        return builder.ToString();
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("TradingIntel.sln").Length > 0)
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
