using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class configRarity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlindBoxItems_RarityConfigs_RarityConfigId",
                table: "BlindBoxItems");

            migrationBuilder.DropForeignKey(
                name: "FK_BlindBoxItems_RarityConfigs_RarityId",
                table: "BlindBoxItems");

            migrationBuilder.DropIndex(
                name: "IX_BlindBoxItems_RarityConfigId",
                table: "BlindBoxItems");

            migrationBuilder.DropIndex(
                name: "IX_BlindBoxItems_RarityId",
                table: "BlindBoxItems");

            migrationBuilder.DropColumn(
                name: "RarityConfigId",
                table: "BlindBoxItems");

            migrationBuilder.DropColumn(
                name: "RarityId",
                table: "BlindBoxItems");

            migrationBuilder.AlterColumn<int>(
                name: "Weight",
                table: "RarityConfigs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "RarityConfigs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "BlindBoxItemId",
                table: "RarityConfigs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_RarityConfigs_BlindBoxItemId",
                table: "RarityConfigs",
                column: "BlindBoxItemId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RarityConfigs_BlindBoxItems_BlindBoxItemId",
                table: "RarityConfigs",
                column: "BlindBoxItemId",
                principalTable: "BlindBoxItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RarityConfigs_BlindBoxItems_BlindBoxItemId",
                table: "RarityConfigs");

            migrationBuilder.DropIndex(
                name: "IX_RarityConfigs_BlindBoxItemId",
                table: "RarityConfigs");

            migrationBuilder.DropColumn(
                name: "BlindBoxItemId",
                table: "RarityConfigs");

            migrationBuilder.AlterColumn<decimal>(
                name: "Weight",
                table: "RarityConfigs",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "RarityConfigs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<Guid>(
                name: "RarityConfigId",
                table: "BlindBoxItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RarityId",
                table: "BlindBoxItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_BlindBoxItems_RarityConfigId",
                table: "BlindBoxItems",
                column: "RarityConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_BlindBoxItems_RarityId",
                table: "BlindBoxItems",
                column: "RarityId");

            migrationBuilder.AddForeignKey(
                name: "FK_BlindBoxItems_RarityConfigs_RarityConfigId",
                table: "BlindBoxItems",
                column: "RarityConfigId",
                principalTable: "RarityConfigs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BlindBoxItems_RarityConfigs_RarityId",
                table: "BlindBoxItems",
                column: "RarityId",
                principalTable: "RarityConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
