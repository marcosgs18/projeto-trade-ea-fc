using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TradingIntel.Infrastructure.Persistence;

/// <summary>
/// Enables <c>dotnet ef migrations</c> commands without a running host.
/// Uses a static dev connection string; runtime connection is configured via DI.
/// </summary>
public sealed class TradingIntelDbContextFactory : IDesignTimeDbContextFactory<TradingIntelDbContext>
{
    public TradingIntelDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<TradingIntelDbContext>()
            .UseSqlite("Data Source=tradingintel-design.db");

        return new TradingIntelDbContext(builder.Options);
    }
}
