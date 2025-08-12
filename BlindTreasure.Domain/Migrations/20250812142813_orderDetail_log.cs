using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindTreasure.Domain.Migrations
{
    /// <inheritdoc />
    public partial class orderDetail_log : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DeliveredAt",
                table: "Shipments",
                newName: "PickedUpAt");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ShippedAt",
                table: "Shipments",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "EstimatedPickupTime",
                table: "Shipments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderDetailInventoryItemLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderDetailId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogContent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LogTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActionType = table.Column<int>(type: "integer", nullable: true),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_OrderDetailInventoryItemLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderDetailInventoryItemLogs_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderDetailInventoryItemLogs_OrderDetails_OrderDetailId",
                        column: x => x.OrderDetailId,
                        principalTable: "OrderDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetailInventoryItemLogs_InventoryItemId",
                table: "OrderDetailInventoryItemLogs",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetailInventoryItemLogs_OrderDetailId",
                table: "OrderDetailInventoryItemLogs",
                column: "OrderDetailId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderDetailInventoryItemLogs");

            migrationBuilder.DropColumn(
                name: "EstimatedPickupTime",
                table: "Shipments");

            migrationBuilder.RenameColumn(
                name: "PickedUpAt",
                table: "Shipments",
                newName: "DeliveredAt");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ShippedAt",
                table: "Shipments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
