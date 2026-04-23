using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingIntel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "market_listing_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ListingId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PlayerId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerDisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartingBid = table.Column<decimal>(type: "TEXT", nullable: false),
                    BuyNowPrice = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_listing_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "player_price_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerDisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BuyNowPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    SellNowPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    MedianMarketPrice = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_price_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "raw_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RecordCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PayloadHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    RawPayload = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raw_snapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_market_listing_snapshots_listing_id",
                table: "market_listing_snapshots",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "ix_market_listing_snapshots_player_captured_at",
                table: "market_listing_snapshots",
                columns: new[] { "PlayerId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_player_price_snapshots_player_captured_at",
                table: "player_price_snapshots",
                columns: new[] { "PlayerId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_player_price_snapshots_source_captured_at",
                table: "player_price_snapshots",
                columns: new[] { "Source", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_raw_snapshots_payload_hash",
                table: "raw_snapshots",
                column: "PayloadHash");

            migrationBuilder.CreateIndex(
                name: "ix_raw_snapshots_source_captured_at",
                table: "raw_snapshots",
                columns: new[] { "Source", "CapturedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "market_listing_snapshots");

            migrationBuilder.DropTable(
                name: "player_price_snapshots");

            migrationBuilder.DropTable(
                name: "raw_snapshots");
        }
    }
}
