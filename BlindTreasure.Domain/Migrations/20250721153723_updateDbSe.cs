using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class updateDbSe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_TradeRequest_LockedByRequestId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TradeHistory_InventoryItems_OfferedInventoryId",
                table: "TradeHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_TradeHistory_Listings_ListingId",
                table: "TradeHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_TradeHistory_Users_RequesterId",
                table: "TradeHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_TradeRequest_InventoryItems_OfferedInventoryId",
                table: "TradeRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_TradeRequest_Listings_ListingId",
                table: "TradeRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_TradeRequest_Users_RequesterId",
                table: "TradeRequest");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TradeRequest",
                table: "TradeRequest");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TradeHistory",
                table: "TradeHistory");

            migrationBuilder.RenameTable(
                name: "TradeRequest",
                newName: "TradeRequests");

            migrationBuilder.RenameTable(
                name: "TradeHistory",
                newName: "TradeHistories");

            migrationBuilder.RenameIndex(
                name: "IX_TradeRequest_RequesterId",
                table: "TradeRequests",
                newName: "IX_TradeRequests_RequesterId");

            migrationBuilder.RenameIndex(
                name: "IX_TradeRequest_OfferedInventoryId",
                table: "TradeRequests",
                newName: "IX_TradeRequests_OfferedInventoryId");

            migrationBuilder.RenameIndex(
                name: "IX_TradeRequest_ListingId",
                table: "TradeRequests",
                newName: "IX_TradeRequests_ListingId");

            migrationBuilder.RenameIndex(
                name: "IX_TradeHistory_RequesterId",
                table: "TradeHistories",
                newName: "IX_TradeHistories_RequesterId");

            migrationBuilder.RenameIndex(
                name: "IX_TradeHistory_OfferedInventoryId",
                table: "TradeHistories",
                newName: "IX_TradeHistories_OfferedInventoryId");

            migrationBuilder.RenameIndex(
                name: "IX_TradeHistory_ListingId",
                table: "TradeHistories",
                newName: "IX_TradeHistories_ListingId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TradeRequests",
                table: "TradeRequests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TradeHistories",
                table: "TradeHistories",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_TradeRequests_LockedByRequestId",
                table: "InventoryItems",
                column: "LockedByRequestId",
                principalTable: "TradeRequests",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TradeHistories_InventoryItems_OfferedInventoryId",
                table: "TradeHistories",
                column: "OfferedInventoryId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TradeHistories_Listings_ListingId",
                table: "TradeHistories",
                column: "ListingId",
                principalTable: "Listings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TradeHistories_Users_RequesterId",
                table: "TradeHistories",
                column: "RequesterId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TradeRequests_InventoryItems_OfferedInventoryId",
                table: "TradeRequests",
                column: "OfferedInventoryId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TradeRequests_Listings_ListingId",
                table: "TradeRequests",
                column: "ListingId",
                principalTable: "Listings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TradeRequests_Users_RequesterId",
                table: "TradeRequests",
                column: "RequesterId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_TradeRequests_LockedByRequestId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TradeHistories_InventoryItems_OfferedInventoryId",
                table: "TradeHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_TradeHistories_Listings_ListingId",
                table: "TradeHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_TradeHistories_Users_RequesterId",
                table: "TradeHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_TradeRequests_InventoryItems_OfferedInventoryId",
                table: "TradeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TradeRequests_Listings_ListingId",
                table: "TradeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TradeRequests_Users_RequesterId",
                table: "TradeRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TradeRequests",
                table: "TradeRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TradeHistories",
                table: "TradeHistories");

            migrationBuilder.RenameTable(
                name: "TradeRequests",
                newName: "TradeRequest");

            migrationBuilder.RenameTable(
                name: "TradeHistories",
                newName: "TradeHistory");

            migrationBuilder.RenameIndex(
                name: "IX_TradeRequests_RequesterId",
                table: "TradeRequest",
                newName: "IX_TradeRequest_RequesterId");

            migrationBuilder.RenameIndex(
                name: "IX_TradeRequests_OfferedInventoryId",
                table: "TradeRequest",
                newName: "IX_TradeRequest_OfferedInventoryId");

            migrationBuilder.RenameIndex(
                name: "IX_TradeRequests_ListingId",
                table: "TradeRequest",
                newName: "IX_TradeRequest_ListingId");

            migrationBuilder.RenameIndex(
                name: "IX_TradeHistories_RequesterId",
                table: "TradeHistory",
                newName: "IX_TradeHistory_RequesterId");

            migrationBuilder.RenameIndex(
                name: "IX_TradeHistories_OfferedInventoryId",
                table: "TradeHistory",
                newName: "IX_TradeHistory_OfferedInventoryId");

            migrationBuilder.RenameIndex(
                name: "IX_TradeHistories_ListingId",
                table: "TradeHistory",
                newName: "IX_TradeHistory_ListingId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TradeRequest",
                table: "TradeRequest",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TradeHistory",
                table: "TradeHistory",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_TradeRequest_LockedByRequestId",
                table: "InventoryItems",
                column: "LockedByRequestId",
                principalTable: "TradeRequest",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TradeHistory_InventoryItems_OfferedInventoryId",
                table: "TradeHistory",
                column: "OfferedInventoryId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TradeHistory_Listings_ListingId",
                table: "TradeHistory",
                column: "ListingId",
                principalTable: "Listings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TradeHistory_Users_RequesterId",
                table: "TradeHistory",
                column: "RequesterId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TradeRequest_InventoryItems_OfferedInventoryId",
                table: "TradeRequest",
                column: "OfferedInventoryId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TradeRequest_Listings_ListingId",
                table: "TradeRequest",
                column: "ListingId",
                principalTable: "Listings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TradeRequest_Users_RequesterId",
                table: "TradeRequest",
                column: "RequesterId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
