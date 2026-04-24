using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingIntel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackedPlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tracked_players",
                columns: table => new
                {
                    PlayerId = table.Column<long>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Overall = table.Column<int>(type: "INTEGER", nullable: true),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastCollectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracked_players", x => x.PlayerId);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tracked_players_is_active",
                table: "tracked_players",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "ix_tracked_players_source",
                table: "tracked_players",
                column: "Source");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tracked_players");
        }
    }
}
