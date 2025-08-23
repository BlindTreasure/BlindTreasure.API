using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class fields_payout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdjustedGrossAmount",
                table: "Payouts",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalRefundAmount",
                table: "Payouts",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                table: "PayoutDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PayoutId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalRefundAmount",
                table: "Orders",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                table: "OrderDetails",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PayoutId",
                table: "Orders",
                column: "PayoutId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Payouts_PayoutId",
                table: "Orders",
                column: "PayoutId",
                principalTable: "Payouts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Payouts_PayoutId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PayoutId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AdjustedGrossAmount",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "TotalRefundAmount",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "PayoutDetails");

            migrationBuilder.DropColumn(
                name: "PayoutId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TotalRefundAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "OrderDetails");
        }
    }
}
