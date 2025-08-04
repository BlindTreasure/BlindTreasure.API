using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class optimize_orderdetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalItemsShippingFee",
                table: "OrderDetails");

            migrationBuilder.AddColumn<decimal>(
                name: "DetailDiscountPromotion",
                table: "OrderDetails",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalDetailPrice",
                table: "OrderDetails",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetailDiscountPromotion",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "FinalDetailPrice",
                table: "OrderDetails");

            migrationBuilder.AddColumn<int>(
                name: "TotalItemsShippingFee",
                table: "OrderDetails",
                type: "integer",
                nullable: true);
        }
    }
}
