using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class payout_transaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayoutTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerName = table.Column<string>(type: "text", nullable: false),
                    StripeTransferId = table.Column<string>(type: "text", nullable: false),
                    StripeDestinationAccount = table.Column<string>(type: "text", nullable: false),
                    StripeBalanceTransactionId = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TransferredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    InitiatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatedByName = table.Column<string>(type: "text", nullable: false),
                    ExternalRef = table.Column<string>(type: "text", nullable: true),
                    BatchId = table.Column<string>(type: "text", nullable: true),
                    PlatformRevenueOfPayoutAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayoutTransactions_Payouts_PayoutId",
                        column: x => x.PayoutId,
                        principalTable: "Payouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutTransactions_PayoutId",
                table: "PayoutTransactions",
                column: "PayoutId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayoutTransactions");
        }
    }
}
