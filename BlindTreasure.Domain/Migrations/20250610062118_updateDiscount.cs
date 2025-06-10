using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class updateDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Promotions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DiscountType",
                table: "Promotions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "SellerId",
                table: "Promotions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_SellerId",
                table: "Promotions",
                column: "SellerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Promotions_Sellers_SellerId",
                table: "Promotions",
                column: "SellerId",
                principalTable: "Sellers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Promotions_Sellers_SellerId",
                table: "Promotions");

            migrationBuilder.DropIndex(
                name: "IX_Promotions_SellerId",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "Promotions");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Promotions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "DiscountType",
                table: "Promotions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);
        }
    }
}
