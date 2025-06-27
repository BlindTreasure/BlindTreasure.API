using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class updateSecretProbabiltyDecimal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "SecretProbability",
                table: "BlindBoxes",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SecretProbability",
                table: "BlindBoxes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");
        }
    }
}
