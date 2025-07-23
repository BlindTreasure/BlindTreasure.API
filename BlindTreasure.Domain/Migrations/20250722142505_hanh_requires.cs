using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class hanh_requires : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shipments_OrderDetails_OrderDetailId",
                table: "Shipments");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrderDetailId",
                table: "Shipments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "TotalShippingFee",
                table: "OrderDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderDetailId",
                table: "InventoryItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_OrderDetailId",
                table: "InventoryItems",
                column: "OrderDetailId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_OrderDetails_OrderDetailId",
                table: "InventoryItems",
                column: "OrderDetailId",
                principalTable: "OrderDetails",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Shipments_OrderDetails_OrderDetailId",
                table: "Shipments",
                column: "OrderDetailId",
                principalTable: "OrderDetails",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_OrderDetails_OrderDetailId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Shipments_OrderDetails_OrderDetailId",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_OrderDetailId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "TotalShippingFee",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "OrderDetailId",
                table: "InventoryItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrderDetailId",
                table: "Shipments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Shipments_OrderDetails_OrderDetailId",
                table: "Shipments",
                column: "OrderDetailId",
                principalTable: "OrderDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
