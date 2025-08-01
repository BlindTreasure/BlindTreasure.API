using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class OrderDetail_Shipment_ManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shipments_OrderDetails_OrderDetailId",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_Shipments_OrderDetailId",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "OrderDetailId",
                table: "Shipments");

            migrationBuilder.CreateTable(
                name: "OrderDetailShipments",
                columns: table => new
                {
                    OrderDetailsId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDetailShipments", x => new { x.OrderDetailsId, x.ShipmentsId });
                    table.ForeignKey(
                        name: "FK_OrderDetailShipments_OrderDetails_OrderDetailsId",
                        column: x => x.OrderDetailsId,
                        principalTable: "OrderDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderDetailShipments_Shipments_ShipmentsId",
                        column: x => x.ShipmentsId,
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetailShipments_ShipmentsId",
                table: "OrderDetailShipments",
                column: "ShipmentsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderDetailShipments");

            migrationBuilder.AddColumn<Guid>(
                name: "OrderDetailId",
                table: "Shipments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_OrderDetailId",
                table: "Shipments",
                column: "OrderDetailId");

            migrationBuilder.AddForeignKey(
                name: "FK_Shipments_OrderDetails_OrderDetailId",
                table: "Shipments",
                column: "OrderDetailId",
                principalTable: "OrderDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
