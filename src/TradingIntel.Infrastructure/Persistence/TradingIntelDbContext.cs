using Microsoft.EntityFrameworkCore;
using TradingIntel.Infrastructure.Persistence.Entities;

namespace TradingIntel.Infrastructure.Persistence;

public sealed class TradingIntelDbContext : DbContext
{
    public TradingIntelDbContext(DbContextOptions<TradingIntelDbContext> options)
        : base(options)
    {
    }

    internal DbSet<RawSnapshotRecord> RawSnapshots => Set<RawSnapshotRecord>();

    internal DbSet<PlayerPriceSnapshotRecord> PlayerPriceSnapshots => Set<PlayerPriceSnapshotRecord>();

    internal DbSet<MarketListingSnapshotRecord> MarketListingSnapshots => Set<MarketListingSnapshotRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RawSnapshotRecord>(entity =>
        {
            entity.ToTable("raw_snapshots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(64);
            entity.Property(e => e.CorrelationId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.PayloadHash).IsRequired().HasMaxLength(128);
            entity.Property(e => e.RawPayload).IsRequired();
            entity.HasIndex(e => new { e.Source, e.CapturedAtUtc }).HasDatabaseName("ix_raw_snapshots_source_captured_at");
            entity.HasIndex(e => e.PayloadHash).HasDatabaseName("ix_raw_snapshots_payload_hash");
        });

        modelBuilder.Entity<PlayerPriceSnapshotRecord>(entity =>
        {
            entity.ToTable("player_price_snapshots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlayerDisplayName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(64);
            entity.Property(e => e.BuyNowPrice).HasColumnType("TEXT");
            entity.Property(e => e.SellNowPrice).HasColumnType("TEXT");
            entity.Property(e => e.MedianMarketPrice).HasColumnType("TEXT");
            entity.HasIndex(e => new { e.PlayerId, e.CapturedAtUtc }).HasDatabaseName("ix_player_price_snapshots_player_captured_at");
            entity.HasIndex(e => new { e.Source, e.CapturedAtUtc }).HasDatabaseName("ix_player_price_snapshots_source_captured_at");
        });

        modelBuilder.Entity<MarketListingSnapshotRecord>(entity =>
        {
            entity.ToTable("market_listing_snapshots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ListingId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.PlayerDisplayName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(64);
            entity.Property(e => e.StartingBid).HasColumnType("TEXT");
            entity.Property(e => e.BuyNowPrice).HasColumnType("TEXT");
            entity.HasIndex(e => new { e.PlayerId, e.CapturedAtUtc }).HasDatabaseName("ix_market_listing_snapshots_player_captured_at");
            entity.HasIndex(e => e.ListingId).HasDatabaseName("ix_market_listing_snapshots_listing_id");
        });
    }
}
