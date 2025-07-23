using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class fixing_field : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "ShippedAt",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "InventoryItems");

            migrationBuilder.AddColumn<Guid>(
                name: "SellerId",
                table: "OrderDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ShipmentId",
                table: "InventoryItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_SellerId",
                table: "OrderDetails",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ShipmentId",
                table: "InventoryItems",
                column: "ShipmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_Shipments_ShipmentId",
                table: "InventoryItems",
                column: "ShipmentId",
                principalTable: "Shipments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_Sellers_SellerId",
                table: "OrderDetails",
                column: "SellerId",
                principalTable: "Sellers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_Shipments_ShipmentId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_Sellers_SellerId",
                table: "OrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_OrderDetails_SellerId",
                table: "OrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_ShipmentId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "ShipmentId",
                table: "InventoryItems");

            migrationBuilder.AddColumn<Guid>(
                name: "SellerId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedAt",
                table: "OrderDetails",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShippedAt",
                table: "OrderDetails",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "InventoryItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
