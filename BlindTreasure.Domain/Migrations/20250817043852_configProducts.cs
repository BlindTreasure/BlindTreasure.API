using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class configProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Stock",
                table: "Products",
                newName: "TotalStockQuantity");

            migrationBuilder.AddColumn<int>(
                name: "ReservedInBlindBox",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservedInBlindBox",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "TotalStockQuantity",
                table: "Products",
                newName: "Stock");
        }
    }
}
