using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class configInventoryItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AddressId",
                table: "InventoryItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFromBlindBox",
                table: "InventoryItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceCustomerBlindBoxId",
                table: "InventoryItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_AddressId",
                table: "InventoryItems",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_SourceCustomerBlindBoxId",
                table: "InventoryItems",
                column: "SourceCustomerBlindBoxId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_Addresses_AddressId",
                table: "InventoryItems",
                column: "AddressId",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_CustomerBlindBoxes_SourceCustomerBlindBoxId",
                table: "InventoryItems",
                column: "SourceCustomerBlindBoxId",
                principalTable: "CustomerBlindBoxes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_Addresses_AddressId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_CustomerBlindBoxes_SourceCustomerBlindBoxId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_AddressId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_SourceCustomerBlindBoxId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "AddressId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IsFromBlindBox",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "SourceCustomerBlindBoxId",
                table: "InventoryItems");
        }
    }
}
