using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class temporary_fix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payouts_Seller_Period",
                table: "Payouts");

            migrationBuilder.DropIndex(
                name: "IX_Payouts_Status",
                table: "Payouts");

            migrationBuilder.DropIndex(
                name: "IX_Payouts_StripeTransferId",
                table: "Payouts");

            migrationBuilder.DropIndex(
                name: "UK_PayoutDetails_OrderDetailId",
                table: "PayoutDetails");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutDetails_OrderDetailId",
                table: "PayoutDetails",
                column: "OrderDetailId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayoutDetails_OrderDetailId",
                table: "PayoutDetails");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_Seller_Period",
                table: "Payouts",
                columns: new[] { "SellerId", "PeriodStart", "PeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_Status",
                table: "Payouts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_StripeTransferId",
                table: "Payouts",
                column: "StripeTransferId");

            migrationBuilder.CreateIndex(
                name: "UK_PayoutDetails_OrderDetailId",
                table: "PayoutDetails",
                column: "OrderDetailId",
                unique: true);
        }
    }
}
