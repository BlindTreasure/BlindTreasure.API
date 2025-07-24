using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class configCustomerFavourite2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerBlindBoxes_BlindBoxes_BlindBoxId",
                table: "CustomerBlindBoxes");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerBlindBoxes_OrderDetails_OrderDetailId",
                table: "CustomerBlindBoxes");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerBlindBoxes_Users_UserId",
                table: "CustomerBlindBoxes");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_CustomerBlindBoxes_SourceCustomerBlindBoxId",
                table: "InventoryItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CustomerBlindBoxes",
                table: "CustomerBlindBoxes");

            migrationBuilder.RenameTable(
                name: "CustomerBlindBoxes",
                newName: "CustomerBlindBox");

            migrationBuilder.RenameIndex(
                name: "IX_CustomerBlindBoxes_UserId",
                table: "CustomerBlindBox",
                newName: "IX_CustomerBlindBox_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_CustomerBlindBoxes_OrderDetailId",
                table: "CustomerBlindBox",
                newName: "IX_CustomerBlindBox_OrderDetailId");

            migrationBuilder.RenameIndex(
                name: "IX_CustomerBlindBoxes_BlindBoxId",
                table: "CustomerBlindBox",
                newName: "IX_CustomerBlindBox_BlindBoxId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CustomerBlindBox",
                table: "CustomerBlindBox",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerBlindBox_BlindBoxes_BlindBoxId",
                table: "CustomerBlindBox",
                column: "BlindBoxId",
                principalTable: "BlindBoxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerBlindBox_OrderDetails_OrderDetailId",
                table: "CustomerBlindBox",
                column: "OrderDetailId",
                principalTable: "OrderDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerBlindBox_Users_UserId",
                table: "CustomerBlindBox",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_CustomerBlindBox_SourceCustomerBlindBoxId",
                table: "InventoryItems",
                column: "SourceCustomerBlindBoxId",
                principalTable: "CustomerBlindBox",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerBlindBox_BlindBoxes_BlindBoxId",
                table: "CustomerBlindBox");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerBlindBox_OrderDetails_OrderDetailId",
                table: "CustomerBlindBox");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerBlindBox_Users_UserId",
                table: "CustomerBlindBox");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_CustomerBlindBox_SourceCustomerBlindBoxId",
                table: "InventoryItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CustomerBlindBox",
                table: "CustomerBlindBox");

            migrationBuilder.RenameTable(
                name: "CustomerBlindBox",
                newName: "CustomerBlindBoxes");

            migrationBuilder.RenameIndex(
                name: "IX_CustomerBlindBox_UserId",
                table: "CustomerBlindBoxes",
                newName: "IX_CustomerBlindBoxes_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_CustomerBlindBox_OrderDetailId",
                table: "CustomerBlindBoxes",
                newName: "IX_CustomerBlindBoxes_OrderDetailId");

            migrationBuilder.RenameIndex(
                name: "IX_CustomerBlindBox_BlindBoxId",
                table: "CustomerBlindBoxes",
                newName: "IX_CustomerBlindBoxes_BlindBoxId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CustomerBlindBoxes",
                table: "CustomerBlindBoxes",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerBlindBoxes_BlindBoxes_BlindBoxId",
                table: "CustomerBlindBoxes",
                column: "BlindBoxId",
                principalTable: "BlindBoxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerBlindBoxes_OrderDetails_OrderDetailId",
                table: "CustomerBlindBoxes",
                column: "OrderDetailId",
                principalTable: "OrderDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerBlindBoxes_Users_UserId",
                table: "CustomerBlindBoxes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_CustomerBlindBoxes_SourceCustomerBlindBoxId",
                table: "InventoryItems",
                column: "SourceCustomerBlindBoxId",
                principalTable: "CustomerBlindBoxes",
                principalColumn: "Id");
        }
    }
}
