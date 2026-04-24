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

    internal DbSet<SbcChallengeRecord> SbcChallenges => Set<SbcChallengeRecord>();

    internal DbSet<SbcChallengeRequirementRecord> SbcChallengeRequirements => Set<SbcChallengeRequirementRecord>();

    internal DbSet<TradeOpportunityRecord> TradeOpportunities => Set<TradeOpportunityRecord>();

    internal DbSet<TrackedPlayerRecord> TrackedPlayers => Set<TrackedPlayerRecord>();

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

        modelBuilder.Entity<SbcChallengeRecord>(entity =>
        {
            entity.ToTable("sbc_challenges");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(128);
            entity.Property(e => e.SetName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.RepeatabilityKind).HasConversion<int>();
            entity.HasIndex(e => e.ExpiresAtUtc).HasDatabaseName("ix_sbc_challenges_expires_at");
            entity.HasIndex(e => e.Category).HasDatabaseName("ix_sbc_challenges_category");

            entity.HasMany(e => e.Requirements)
                .WithOne(r => r.Challenge)
                .HasForeignKey(r => r.ChallengeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SbcChallengeRequirementRecord>(entity =>
        {
            entity.ToTable("sbc_challenge_requirements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.ChallengeId).HasDatabaseName("ix_sbc_challenge_requirements_challenge_id");
            entity.HasIndex(e => new { e.Key, e.Minimum }).HasDatabaseName("ix_sbc_challenge_requirements_key_minimum");
        });

        modelBuilder.Entity<TradeOpportunityRecord>(entity =>
        {
            entity.ToTable("trade_opportunities");
            entity.HasKey(e => e.PlayerId);
            entity.Property(e => e.PlayerId).ValueGeneratedNever();
            entity.Property(e => e.PlayerDisplayName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ExpectedBuyPrice).HasColumnType("TEXT");
            entity.Property(e => e.ExpectedSellPrice).HasColumnType("TEXT");
            entity.Property(e => e.ExpectedNetMargin).HasColumnType("TEXT");
            entity.Property(e => e.Confidence).HasColumnType("TEXT");
            entity.Property(e => e.ReasonsJson).IsRequired();
            entity.Property(e => e.SuggestionsJson).IsRequired();
            entity.HasIndex(e => e.LastRecomputedAtUtc).HasDatabaseName("ix_trade_opportunities_last_recomputed");
            entity.HasIndex(e => e.IsStale).HasDatabaseName("ix_trade_opportunities_is_stale");
            entity.HasIndex(e => e.OpportunityId).HasDatabaseName("ix_trade_opportunities_opportunity_id");
        });

        modelBuilder.Entity<TrackedPlayerRecord>(entity =>
        {
            entity.ToTable("tracked_players");
            entity.HasKey(e => e.PlayerId);
            entity.Property(e => e.PlayerId).ValueGeneratedNever();
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Source).HasConversion<int>();
            entity.HasIndex(e => e.IsActive).HasDatabaseName("ix_tracked_players_is_active");
            entity.HasIndex(e => e.Source).HasDatabaseName("ix_tracked_players_source");
        });
    }
}
