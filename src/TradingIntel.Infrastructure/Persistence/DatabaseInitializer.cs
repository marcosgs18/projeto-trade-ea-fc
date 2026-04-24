using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace TradingIntel.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies pending EF Core migrations against the resolved
    /// <see cref="TradingIntelDbContext"/>. Safe to call on every startup
    /// (<c>Database.Migrate()</c> is idempotent) and intended as a first-run
    /// convenience for Worker and API hosts sharing the same SQLite file.
    /// </summary>
    public static void MigrateTradingIntelDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TradingIntelDbContext>();
        db.Database.Migrate();
    }
}
