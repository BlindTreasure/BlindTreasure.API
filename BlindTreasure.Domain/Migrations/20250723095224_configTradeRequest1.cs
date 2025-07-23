using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class configTradeRequest1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TradeRequests_InventoryItems_OfferedInventoryId",
                table: "TradeRequests");

            migrationBuilder.DropIndex(
                name: "IX_TradeRequests_OfferedInventoryId",
                table: "TradeRequests");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_LockedByRequestId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "OfferedInventoryId",
                table: "TradeRequests");

            migrationBuilder.CreateTable(
                name: "TradeRequestItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TradeRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_TradeRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeRequestItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TradeRequestItems_TradeRequests_TradeRequestId",
                        column: x => x.TradeRequestId,
                        principalTable: "TradeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_LockedByRequestId",
                table: "InventoryItems",
                column: "LockedByRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeRequestItems_InventoryItemId",
                table: "TradeRequestItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeRequestItems_TradeRequestId",
                table: "TradeRequestItems",
                column: "TradeRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradeRequestItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_LockedByRequestId",
                table: "InventoryItems");

            migrationBuilder.AddColumn<Guid>(
                name: "OfferedInventoryId",
                table: "TradeRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeRequests_OfferedInventoryId",
                table: "TradeRequests",
                column: "OfferedInventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_LockedByRequestId",
                table: "InventoryItems",
                column: "LockedByRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_TradeRequests_InventoryItems_OfferedInventoryId",
                table: "TradeRequests",
                column: "OfferedInventoryId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
