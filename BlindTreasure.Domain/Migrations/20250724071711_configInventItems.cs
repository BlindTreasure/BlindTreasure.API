using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class configInventItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HoldUntil",
                table: "InventoryItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastTradeHistoryId",
                table: "InventoryItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_LastTradeHistoryId",
                table: "InventoryItems",
                column: "LastTradeHistoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_TradeHistories_LastTradeHistoryId",
                table: "InventoryItems",
                column: "LastTradeHistoryId",
                principalTable: "TradeHistories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_TradeHistories_LastTradeHistoryId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_LastTradeHistoryId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "HoldUntil",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "LastTradeHistoryId",
                table: "InventoryItems");
        }
    }
}
