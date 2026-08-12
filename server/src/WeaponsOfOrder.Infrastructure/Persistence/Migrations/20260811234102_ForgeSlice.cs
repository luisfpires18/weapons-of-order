using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeaponsOfOrder.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ForgeSlice : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ForgeSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                RecipeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Temperature = table.Column<double>(type: "double precision", nullable: false),
                TemperatureAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                IsHeating = table.Column<bool>(type: "boolean", nullable: false),
                BurnSeconds = table.Column<double>(type: "double precision", nullable: false),
                StrikesTaken = table.Column<int>(type: "integer", nullable: false),
                LastStrikeAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Craftsmanship = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ForgeSessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_ForgeSessions_AspNetUsers_OwnerUserId",
                    column: x => x.OwnerUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PlayerMaterials",
            columns: table => new
            {
                OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Metal = table.Column<int>(type: "integer", nullable: false),
                Wood = table.Column<int>(type: "integer", nullable: false),
                Leather = table.Column<int>(type: "integer", nullable: false),
                GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlayerMaterials", x => x.OwnerUserId);
                table.CheckConstraint("CK_PlayerMaterials_NonNegative", "\"Metal\" >= 0 AND \"Wood\" >= 0 AND \"Leather\" >= 0");
                table.ForeignKey(
                    name: "FK_PlayerMaterials_AspNetUsers_OwnerUserId",
                    column: x => x.OwnerUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ForgedItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                ForgeSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                RecipeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                WeaponType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Craftsmanship = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ForgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ForgedItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_ForgedItems_AspNetUsers_OwnerUserId",
                    column: x => x.OwnerUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ForgedItems_ForgeSessions_ForgeSessionId",
                    column: x => x.ForgeSessionId,
                    principalTable: "ForgeSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ForgeStrikes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ForgeSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                Ordinal = table.Column<int>(type: "integer", nullable: false),
                Band = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Temperature = table.Column<double>(type: "double precision", nullable: false),
                StruckAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ForgeStrikes", x => x.Id);
                table.ForeignKey(
                    name: "FK_ForgeStrikes_ForgeSessions_ForgeSessionId",
                    column: x => x.ForgeSessionId,
                    principalTable: "ForgeSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ForgedItems_ForgeSessionId",
            table: "ForgedItems",
            column: "ForgeSessionId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ForgedItems_OwnerUserId_ForgedAt",
            table: "ForgedItems",
            columns: new[] { "OwnerUserId", "ForgedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ForgeSessions_OwnerUserId_Active",
            table: "ForgeSessions",
            column: "OwnerUserId",
            unique: true,
            filter: "\"Status\" = 'Active'");

        migrationBuilder.CreateIndex(
            name: "IX_ForgeSessions_OwnerUserId_StartedAt",
            table: "ForgeSessions",
            columns: new[] { "OwnerUserId", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ForgeStrikes_ForgeSessionId_Ordinal",
            table: "ForgeStrikes",
            columns: new[] { "ForgeSessionId", "Ordinal" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ForgedItems");

        migrationBuilder.DropTable(
            name: "ForgeStrikes");

        migrationBuilder.DropTable(
            name: "PlayerMaterials");

        migrationBuilder.DropTable(
            name: "ForgeSessions");
    }
}
