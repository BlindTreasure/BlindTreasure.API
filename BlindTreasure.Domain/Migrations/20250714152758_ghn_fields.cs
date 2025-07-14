using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class ghn_fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MainServiceFee",
                table: "Shipments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderCode",
                table: "Shipments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalFee",
                table: "Shipments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyDistrictName",
                table: "Sellers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyPhone",
                table: "Sellers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyProvinceName",
                table: "Sellers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyWardName",
                table: "Sellers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Length",
                table: "Products",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "Products",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Width",
                table: "Products",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "Addresses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ward",
                table: "Addresses",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MainServiceFee",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "OrderCode",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "TotalFee",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "CompanyDistrictName",
                table: "Sellers");

            migrationBuilder.DropColumn(
                name: "CompanyPhone",
                table: "Sellers");

            migrationBuilder.DropColumn(
                name: "CompanyProvinceName",
                table: "Sellers");

            migrationBuilder.DropColumn(
                name: "CompanyWardName",
                table: "Sellers");

            migrationBuilder.DropColumn(
                name: "Length",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "District",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "Ward",
                table: "Addresses");
        }
    }
}
