using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingIntel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSbcChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sbc_challenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RepeatabilityKind = table.Column<int>(type: "INTEGER", nullable: false),
                    RepeatabilityMaxCompletions = table.Column<int>(type: "INTEGER", nullable: true),
                    SetName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ObservedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sbc_challenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sbc_challenge_requirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Minimum = table.Column<int>(type: "INTEGER", nullable: false),
                    Maximum = table.Column<int>(type: "INTEGER", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sbc_challenge_requirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sbc_challenge_requirements_sbc_challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "sbc_challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sbc_challenge_requirements_challenge_id",
                table: "sbc_challenge_requirements",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "ix_sbc_challenge_requirements_key_minimum",
                table: "sbc_challenge_requirements",
                columns: new[] { "Key", "Minimum" });

            migrationBuilder.CreateIndex(
                name: "ix_sbc_challenges_category",
                table: "sbc_challenges",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "ix_sbc_challenges_expires_at",
                table: "sbc_challenges",
                column: "ExpiresAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sbc_challenge_requirements");

            migrationBuilder.DropTable(
                name: "sbc_challenges");
        }
    }
}
