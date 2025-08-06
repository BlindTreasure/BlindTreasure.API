using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class configReview2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryRating",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "QualityRating",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ServiceRating",
                table: "Reviews");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeliveryRating",
                table: "Reviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QualityRating",
                table: "Reviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceRating",
                table: "Reviews",
                type: "integer",
                nullable: true);
        }
    }
}
