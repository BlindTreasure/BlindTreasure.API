using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class configCustomerFavourite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ProbabilityTableJson",
                table: "BlindBoxUnboxLogs",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "BlindBoxUnboxLogs",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerFavourite",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    BlindBoxId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_CustomerFavourite", x => x.Id);
                    table.CheckConstraint("CK_CustomerFavourite_OneTypeOnly", "(\"ProductId\" IS NOT NULL AND \"BlindBoxId\" IS NULL) OR (\"ProductId\" IS NULL AND \"BlindBoxId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CustomerFavourite_BlindBoxes_BlindBoxId",
                        column: x => x.BlindBoxId,
                        principalTable: "BlindBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerFavourite_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerFavourite_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlindBoxUnboxLogs_UserId",
                table: "BlindBoxUnboxLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFavourite_BlindBoxId",
                table: "CustomerFavourite",
                column: "BlindBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFavourite_ProductId",
                table: "CustomerFavourite",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFavourite_UserId_ProductId_BlindBoxId",
                table: "CustomerFavourite",
                columns: new[] { "UserId", "ProductId", "BlindBoxId" },
                unique: true,
                filter: "\"ProductId\" IS NOT NULL OR \"BlindBoxId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_BlindBoxUnboxLogs_Users_UserId",
                table: "BlindBoxUnboxLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlindBoxUnboxLogs_Users_UserId",
                table: "BlindBoxUnboxLogs");

            migrationBuilder.DropTable(
                name: "CustomerFavourite");

            migrationBuilder.DropIndex(
                name: "IX_BlindBoxUnboxLogs_UserId",
                table: "BlindBoxUnboxLogs");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "BlindBoxUnboxLogs");

            migrationBuilder.AlterColumn<string>(
                name: "ProbabilityTableJson",
                table: "BlindBoxUnboxLogs",
                type: "jsonb",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);
        }
    }
}
