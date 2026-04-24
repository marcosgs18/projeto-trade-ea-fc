using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingIntel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TradeOpportunityNetMarginAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "PlayerId",
                table: "trade_opportunities",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedNetMargin",
                table: "trade_opportunities",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "ix_trade_opportunities_opportunity_id",
                table: "trade_opportunities",
                column: "OpportunityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_trade_opportunities_opportunity_id",
                table: "trade_opportunities");

            migrationBuilder.DropColumn(
                name: "ExpectedNetMargin",
                table: "trade_opportunities");

            migrationBuilder.AlterColumn<long>(
                name: "PlayerId",
                table: "trade_opportunities",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);
        }
    }
}
