using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class configListing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Price",
                table: "Listings");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Listings",
                type: "text",
                nullable: true);

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

            migrationBuilder.AddColumn<bool>(
                name: "IsFree",
                table: "Listings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TradeStatus",
                table: "Listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ListingReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_ListingReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingReports_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ListingReports_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListingReports_ListingId",
                table: "ListingReports",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingReports_UserId",
                table: "ListingReports",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListingReports");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "DesiredItemId",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "DesiredItemName",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "IsFree",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "TradeStatus",
                table: "Listings");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Listings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
