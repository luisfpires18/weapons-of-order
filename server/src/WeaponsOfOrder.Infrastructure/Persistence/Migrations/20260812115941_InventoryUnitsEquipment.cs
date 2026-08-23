using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeaponsOfOrder.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InventoryUnitsEquipment : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint(
            name: "AK_ForgedItems_Id_OwnerUserId",
            table: "ForgedItems",
            columns: new[] { "Id", "OwnerUserId" });

        migrationBuilder.CreateTable(
            name: "PlayerUnits",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                DefinitionKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                StarterGrantKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                Origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                AcquiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlayerUnits", x => x.Id);
                table.UniqueConstraint("AK_PlayerUnits_Id_OwnerUserId", x => new { x.Id, x.OwnerUserId });
                table.ForeignKey(
                    name: "FK_PlayerUnits_AspNetUsers_OwnerUserId",
                    column: x => x.OwnerUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "EquippedWeapons",
            columns: table => new
            {
                ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                PlayerUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                OccupiesFirstSlot = table.Column<bool>(type: "boolean", nullable: false),
                OccupiesSecondSlot = table.Column<bool>(type: "boolean", nullable: false),
                EquippedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EquippedWeapons", x => x.ItemId);
                table.CheckConstraint("CK_EquippedWeapons_OccupiesASlot", "\"OccupiesFirstSlot\" OR \"OccupiesSecondSlot\"");
                table.ForeignKey(
                    name: "FK_EquippedWeapons_ForgedItems_ItemId_OwnerUserId",
                    columns: x => new { x.ItemId, x.OwnerUserId },
                    principalTable: "ForgedItems",
                    principalColumns: new[] { "Id", "OwnerUserId" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_EquippedWeapons_PlayerUnits_PlayerUnitId_OwnerUserId",
                    columns: x => new { x.PlayerUnitId, x.OwnerUserId },
                    principalTable: "PlayerUnits",
                    principalColumns: new[] { "Id", "OwnerUserId" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EquippedWeapons_ItemId_OwnerUserId",
            table: "EquippedWeapons",
            columns: new[] { "ItemId", "OwnerUserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EquippedWeapons_OwnerUserId",
            table: "EquippedWeapons",
            column: "OwnerUserId");

        migrationBuilder.CreateIndex(
            name: "IX_EquippedWeapons_PlayerUnitId_FirstSlot",
            table: "EquippedWeapons",
            column: "PlayerUnitId",
            unique: true,
            filter: "\"OccupiesFirstSlot\"");

        migrationBuilder.CreateIndex(
            name: "IX_EquippedWeapons_PlayerUnitId_OwnerUserId",
            table: "EquippedWeapons",
            columns: new[] { "PlayerUnitId", "OwnerUserId" });

        migrationBuilder.CreateIndex(
            name: "IX_EquippedWeapons_PlayerUnitId_SecondSlot",
            table: "EquippedWeapons",
            column: "PlayerUnitId",
            unique: true,
            filter: "\"OccupiesSecondSlot\"");

        migrationBuilder.CreateIndex(
            name: "IX_PlayerUnits_OwnerUserId_AcquiredAt",
            table: "PlayerUnits",
            columns: new[] { "OwnerUserId", "AcquiredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PlayerUnits_OwnerUserId_StarterGrantKey",
            table: "PlayerUnits",
            columns: new[] { "OwnerUserId", "StarterGrantKey" },
            unique: true,
            filter: "\"StarterGrantKey\" IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EquippedWeapons");

        migrationBuilder.DropTable(
            name: "PlayerUnits");

        migrationBuilder.DropUniqueConstraint(
            name: "AK_ForgedItems_Id_OwnerUserId",
            table: "ForgedItems");
    }
}
