using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class configTrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LockedAt",
                table: "TradeRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LockedByRequestId",
                table: "InventoryItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_LockedByRequestId",
                table: "InventoryItems",
                column: "LockedByRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_TradeRequest_LockedByRequestId",
                table: "InventoryItems",
                column: "LockedByRequestId",
                principalTable: "TradeRequest",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_TradeRequest_LockedByRequestId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_LockedByRequestId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "LockedAt",
                table: "TradeRequest");

            migrationBuilder.DropColumn(
                name: "LockedByRequestId",
                table: "InventoryItems");
        }
    }
}
