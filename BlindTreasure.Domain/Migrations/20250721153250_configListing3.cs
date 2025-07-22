using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class configListing3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DesiredItemId",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "DesiredItemName",
                table: "Listings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DesiredItemId",
                table: "Listings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DesiredItemName",
                table: "Listings",
                type: "text",
                nullable: true);
        }
    }
}
