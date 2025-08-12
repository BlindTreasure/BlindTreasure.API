using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class nullable_field : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetailInventoryItemLogs_InventoryItems_InventoryItemId",
                table: "OrderDetailInventoryItemLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetailInventoryItemLogs_OrderDetails_OrderDetailId",
                table: "OrderDetailInventoryItemLogs");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrderDetailId",
                table: "OrderDetailInventoryItemLogs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "InventoryItemId",
                table: "OrderDetailInventoryItemLogs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetailInventoryItemLogs_InventoryItems_InventoryItemId",
                table: "OrderDetailInventoryItemLogs",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetailInventoryItemLogs_OrderDetails_OrderDetailId",
                table: "OrderDetailInventoryItemLogs",
                column: "OrderDetailId",
                principalTable: "OrderDetails",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetailInventoryItemLogs_InventoryItems_InventoryItemId",
                table: "OrderDetailInventoryItemLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetailInventoryItemLogs_OrderDetails_OrderDetailId",
                table: "OrderDetailInventoryItemLogs");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrderDetailId",
                table: "OrderDetailInventoryItemLogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "InventoryItemId",
                table: "OrderDetailInventoryItemLogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetailInventoryItemLogs_InventoryItems_InventoryItemId",
                table: "OrderDetailInventoryItemLogs",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetailInventoryItemLogs_OrderDetails_OrderDetailId",
                table: "OrderDetailInventoryItemLogs",
                column: "OrderDetailId",
                principalTable: "OrderDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
