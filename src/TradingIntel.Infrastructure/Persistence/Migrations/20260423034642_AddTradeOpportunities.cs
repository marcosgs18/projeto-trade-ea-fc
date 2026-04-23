using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingIntel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeOpportunities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trade_opportunities",
                columns: table => new
                {
                    PlayerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OpportunityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerDisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpectedBuyPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    ExpectedSellPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    Confidence = table.Column<decimal>(type: "TEXT", nullable: false),
                    ReasonsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SuggestionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    LastRecomputedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsStale = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trade_opportunities", x => x.PlayerId);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trade_opportunities_is_stale",
                table: "trade_opportunities",
                column: "IsStale");

            migrationBuilder.CreateIndex(
                name: "ix_trade_opportunities_last_recomputed",
                table: "trade_opportunities",
                column: "LastRecomputedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trade_opportunities");
        }
    }
}
