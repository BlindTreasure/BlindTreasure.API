using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class products : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Price",
                table: "Products",
                newName: "RealSellingPrice");

            migrationBuilder.AddColumn<decimal>(
                name: "ListedPrice",
                table: "Products",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ListedPrice",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "RealSellingPrice",
                table: "Products",
                newName: "Price");
        }
    }
}
