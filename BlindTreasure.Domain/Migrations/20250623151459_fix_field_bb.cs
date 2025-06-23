using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class fix_field_bb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlindBoxes_Categories_CategoryId",
                table: "BlindBoxes");

            migrationBuilder.DropIndex(
                name: "IX_BlindBoxes_CategoryId",
                table: "BlindBoxes");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "BlindBoxes");

            migrationBuilder.AddColumn<string>(
                name: "BindBoxTags",
                table: "BlindBoxes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BindBoxTags",
                table: "BlindBoxes");

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "BlindBoxes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_BlindBoxes_CategoryId",
                table: "BlindBoxes",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_BlindBoxes_Categories_CategoryId",
                table: "BlindBoxes",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
