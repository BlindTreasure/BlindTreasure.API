using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class akakakaka : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rarity",
                table: "BlindBoxItems");

            migrationBuilder.AddColumn<bool>(
                name: "IsSecret",
                table: "BlindBoxItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

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

            migrationBuilder.CreateTable(
                name: "RarityConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric", nullable: false),
                    IsSecret = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RarityConfigs", x => x.Id);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlindBoxItems_RarityConfigs_RarityConfigId",
                table: "BlindBoxItems");

            migrationBuilder.DropForeignKey(
                name: "FK_BlindBoxItems_RarityConfigs_RarityId",
                table: "BlindBoxItems");

            migrationBuilder.DropTable(
                name: "RarityConfigs");

            migrationBuilder.DropIndex(
                name: "IX_BlindBoxItems_RarityConfigId",
                table: "BlindBoxItems");

            migrationBuilder.DropIndex(
                name: "IX_BlindBoxItems_RarityId",
                table: "BlindBoxItems");

            migrationBuilder.DropColumn(
                name: "IsSecret",
                table: "BlindBoxItems");

            migrationBuilder.DropColumn(
                name: "RarityConfigId",
                table: "BlindBoxItems");

            migrationBuilder.DropColumn(
                name: "RarityId",
                table: "BlindBoxItems");

            migrationBuilder.AddColumn<string>(
                name: "Rarity",
                table: "BlindBoxItems",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }
    }
}
