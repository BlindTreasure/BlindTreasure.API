using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class updateCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlindBoxes_Categories_CategoryId",
                table: "BlindBoxes");

            migrationBuilder.AddForeignKey(
                name: "FK_BlindBoxes_Categories_CategoryId",
                table: "BlindBoxes",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlindBoxes_Categories_CategoryId",
                table: "BlindBoxes");

            migrationBuilder.AddForeignKey(
                name: "FK_BlindBoxes_Categories_CategoryId",
                table: "BlindBoxes",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
