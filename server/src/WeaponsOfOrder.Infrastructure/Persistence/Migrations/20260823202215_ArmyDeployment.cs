using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeaponsOfOrder.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ArmyDeployment : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ArmyPlacements",
            columns: table => new
            {
                OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                PlayerUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                HexColumn = table.Column<int>(type: "integer", nullable: true),
                HexRow = table.Column<int>(type: "integer", nullable: true),
                ReserveOrder = table.Column<int>(type: "integer", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ArmyPlacements", x => new { x.OwnerUserId, x.PlayerUnitId });
                table.CheckConstraint("CK_ArmyPlacements_OwnHalf", "\"HexColumn\" IS NULL\nOR (\"HexColumn\" >= 0 AND \"HexColumn\" < 4\n    AND \"HexRow\" >= 0 AND \"HexRow\" < 7)");
                table.CheckConstraint("CK_ArmyPlacements_ReserveOrder", "\"ReserveOrder\" IS NULL OR \"ReserveOrder\" >= 0");
                table.CheckConstraint("CK_ArmyPlacements_RoleShape", "(\"Role\" = 'Active' AND \"HexColumn\" IS NOT NULL AND \"HexRow\" IS NOT NULL AND \"ReserveOrder\" IS NULL)\nOR (\"Role\" = 'Reserve' AND \"HexColumn\" IS NULL AND \"HexRow\" IS NULL AND \"ReserveOrder\" IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_ArmyPlacements_PlayerUnits_PlayerUnitId_OwnerUserId",
                    columns: x => new { x.PlayerUnitId, x.OwnerUserId },
                    principalTable: "PlayerUnits",
                    principalColumns: new[] { "Id", "OwnerUserId" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ArmyPlacements_OwnerUserId_Hex",
            table: "ArmyPlacements",
            columns: new[] { "OwnerUserId", "HexColumn", "HexRow" },
            unique: true,
            filter: "\"Role\" = 'Active'");

        migrationBuilder.CreateIndex(
            name: "IX_ArmyPlacements_OwnerUserId_ReserveOrder",
            table: "ArmyPlacements",
            columns: new[] { "OwnerUserId", "ReserveOrder" },
            unique: true,
            filter: "\"Role\" = 'Reserve'");

        migrationBuilder.CreateIndex(
            name: "IX_ArmyPlacements_PlayerUnitId_OwnerUserId",
            table: "ArmyPlacements",
            columns: new[] { "PlayerUnitId", "OwnerUserId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ArmyPlacements");
    }
}
