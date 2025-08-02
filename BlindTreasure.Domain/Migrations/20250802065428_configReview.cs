using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class configReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_BlindBoxes_BlindBoxId",
                table: "Reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Products_ProductId",
                table: "Reviews");

            migrationBuilder.RenameColumn(
                name: "Rating",
                table: "Reviews",
                newName: "OverallRating");

            migrationBuilder.RenameColumn(
                name: "Comment",
                table: "Reviews",
                newName: "ImageUrls");

            migrationBuilder.AddColumn<DateTime>(
                name: "AiValidatedAt",
                table: "Reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiValidationDetails",
                table: "Reviews",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryRating",
                table: "Reviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCommentValid",
                table: "Reviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVerifiedPurchase",
                table: "Reviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderDetailId",
                table: "Reviews",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "OriginalComment",
                table: "Reviews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProcessedComment",
                table: "Reviews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QualityRating",
                table: "Reviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SellerId",
                table: "Reviews",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "SellerResponse",
                table: "Reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SellerResponseDate",
                table: "Reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceRating",
                table: "Reviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Reviews",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ValidationReason",
                table: "Reviews",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_OrderDetailId",
                table: "Reviews",
                column: "OrderDetailId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_SellerId",
                table: "Reviews",
                column: "SellerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_BlindBoxes_BlindBoxId",
                table: "Reviews",
                column: "BlindBoxId",
                principalTable: "BlindBoxes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_OrderDetails_OrderDetailId",
                table: "Reviews",
                column: "OrderDetailId",
                principalTable: "OrderDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Products_ProductId",
                table: "Reviews",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Sellers_SellerId",
                table: "Reviews",
                column: "SellerId",
                principalTable: "Sellers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_BlindBoxes_BlindBoxId",
                table: "Reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_OrderDetails_OrderDetailId",
                table: "Reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Products_ProductId",
                table: "Reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Sellers_SellerId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_OrderDetailId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_SellerId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "AiValidatedAt",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "AiValidationDetails",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "DeliveryRating",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "IsCommentValid",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "IsVerifiedPurchase",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "OrderDetailId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "OriginalComment",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ProcessedComment",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "QualityRating",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "SellerResponse",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "SellerResponseDate",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ServiceRating",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ValidationReason",
                table: "Reviews");

            migrationBuilder.RenameColumn(
                name: "OverallRating",
                table: "Reviews",
                newName: "Rating");

            migrationBuilder.RenameColumn(
                name: "ImageUrls",
                table: "Reviews",
                newName: "Comment");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_BlindBoxes_BlindBoxId",
                table: "Reviews",
                column: "BlindBoxId",
                principalTable: "BlindBoxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Products_ProductId",
                table: "Reviews",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
