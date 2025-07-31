using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class update_orderdetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyProductDescription",
                table: "Sellers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Logs",
                table: "OrderDetails",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyProductDescription",
                table: "Sellers");

            migrationBuilder.DropColumn(
                name: "Logs",
                table: "OrderDetails");
        }
    }
}
