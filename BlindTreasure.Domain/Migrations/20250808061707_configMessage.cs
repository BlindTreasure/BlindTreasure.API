using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class configMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileMimeType",
                table: "ChatMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "ChatMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileSize",
                table: "ChatMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileUrl",
                table: "ChatMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryItemId",
                table: "ChatMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_InventoryItemId",
                table: "ChatMessages",
                column: "InventoryItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_InventoryItems_InventoryItemId",
                table: "ChatMessages",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_InventoryItems_InventoryItemId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_InventoryItemId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "FileMimeType",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "FileUrl",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "InventoryItemId",
                table: "ChatMessages");
        }
    }
}
