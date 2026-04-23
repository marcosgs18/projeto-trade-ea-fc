using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TradingIntel.Infrastructure.Persistence;

namespace TradingIntel.Tests.Infrastructure.Persistence;

/// <summary>
/// Shared in-memory SQLite setup: one open connection keeps the database alive
/// for the duration of the fixture and applies migrations so we exercise the
/// same schema produced by <c>dotnet ef migrations</c>.
/// </summary>
public sealed class PersistenceTestFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TradingIntelDbContext> _options;

    public PersistenceTestFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<TradingIntelDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = CreateContext();
        ctx.Database.Migrate();
    }

    public DbContextOptions<TradingIntelDbContext> DbContextOptions => _options;

    public TradingIntelDbContext CreateContext() => new(_options);

    public void Dispose()
    {
        _connection.Dispose();
    }
}
