using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class configPromotion2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PromotionParticipant_Promotions_PromotionId",
                table: "PromotionParticipant");

            migrationBuilder.DropForeignKey(
                name: "FK_PromotionParticipant_Sellers_SellerId",
                table: "PromotionParticipant");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PromotionParticipant",
                table: "PromotionParticipant");

            migrationBuilder.RenameTable(
                name: "PromotionParticipant",
                newName: "PromotionParticipants");

            migrationBuilder.RenameIndex(
                name: "IX_PromotionParticipant_SellerId",
                table: "PromotionParticipants",
                newName: "IX_PromotionParticipants_SellerId");

            migrationBuilder.RenameIndex(
                name: "IX_PromotionParticipant_PromotionId_SellerId",
                table: "PromotionParticipants",
                newName: "IX_PromotionParticipants_PromotionId_SellerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PromotionParticipants",
                table: "PromotionParticipants",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PromotionParticipants_Promotions_PromotionId",
                table: "PromotionParticipants",
                column: "PromotionId",
                principalTable: "Promotions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PromotionParticipants_Sellers_SellerId",
                table: "PromotionParticipants",
                column: "SellerId",
                principalTable: "Sellers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PromotionParticipants_Promotions_PromotionId",
                table: "PromotionParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_PromotionParticipants_Sellers_SellerId",
                table: "PromotionParticipants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PromotionParticipants",
                table: "PromotionParticipants");

            migrationBuilder.RenameTable(
                name: "PromotionParticipants",
                newName: "PromotionParticipant");

            migrationBuilder.RenameIndex(
                name: "IX_PromotionParticipants_SellerId",
                table: "PromotionParticipant",
                newName: "IX_PromotionParticipant_SellerId");

            migrationBuilder.RenameIndex(
                name: "IX_PromotionParticipants_PromotionId_SellerId",
                table: "PromotionParticipant",
                newName: "IX_PromotionParticipant_PromotionId_SellerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PromotionParticipant",
                table: "PromotionParticipant",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PromotionParticipant_Promotions_PromotionId",
                table: "PromotionParticipant",
                column: "PromotionId",
                principalTable: "Promotions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PromotionParticipant_Sellers_SellerId",
                table: "PromotionParticipant",
                column: "SellerId",
                principalTable: "Sellers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
