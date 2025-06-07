using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Add_RejectReason_To_BlindBox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "BlindBoxes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "BlindBoxes");
        }
    }
}
