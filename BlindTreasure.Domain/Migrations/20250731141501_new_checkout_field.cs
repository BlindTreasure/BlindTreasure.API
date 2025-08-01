using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class new_checkout_field : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalShippingFee",
                table: "OrderDetails",
                newName: "TotalItemsShippingFee");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalShippingFee",
                table: "Orders",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalShippingFee",
                table: "Orders");

            migrationBuilder.RenameColumn(
                name: "TotalItemsShippingFee",
                table: "OrderDetails",
                newName: "TotalShippingFee");
        }
    }
}
