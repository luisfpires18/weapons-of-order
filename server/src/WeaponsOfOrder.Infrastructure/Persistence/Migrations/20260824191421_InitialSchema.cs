using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeaponsOfOrder.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AspNetUsers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                LockoutEnd = table.Column<long>(type: "INTEGER", nullable: true),
                LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUsers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserLogins",
            columns: table => new
            {
                LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey(
                    name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserTokens",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Value = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                table.ForeignKey(
                    name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ForgeSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                RecipeKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                Temperature = table.Column<double>(type: "REAL", nullable: false),
                TemperatureAt = table.Column<long>(type: "INTEGER", nullable: false),
                IsHeating = table.Column<bool>(type: "INTEGER", nullable: false),
                BurnSeconds = table.Column<double>(type: "REAL", nullable: false),
                StrikesTaken = table.Column<int>(type: "INTEGER", nullable: false),
                LastStrikeAt = table.Column<long>(type: "INTEGER", nullable: true),
                FinishedAt = table.Column<long>(type: "INTEGER", nullable: true),
                Craftsmanship = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true)
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
                OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                Metal = table.Column<int>(type: "INTEGER", nullable: false),
                Wood = table.Column<int>(type: "INTEGER", nullable: false),
                Leather = table.Column<int>(type: "INTEGER", nullable: false),
                GrantedAt = table.Column<long>(type: "INTEGER", nullable: false)
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
            name: "PlayerUnits",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                DefinitionKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                StarterGrantKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                Origin = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                AcquiredAt = table.Column<long>(type: "INTEGER", nullable: false)
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
            name: "ForgedItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                ForgeSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                RecipeKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                WeaponType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Craftsmanship = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Origin = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                ForgedAt = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ForgedItems", x => x.Id);
                table.UniqueConstraint("AK_ForgedItems_Id_OwnerUserId", x => new { x.Id, x.OwnerUserId });
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
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ForgeSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                Band = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Temperature = table.Column<double>(type: "REAL", nullable: false),
                StruckAt = table.Column<long>(type: "INTEGER", nullable: false)
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

        migrationBuilder.CreateTable(
            name: "ArmyPlacements",
            columns: table => new
            {
                OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                PlayerUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                Role = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                HexColumn = table.Column<int>(type: "INTEGER", nullable: true),
                HexRow = table.Column<int>(type: "INTEGER", nullable: true),
                ReserveOrder = table.Column<int>(type: "INTEGER", nullable: true),
                UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
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

        migrationBuilder.CreateTable(
            name: "EquippedWeapons",
            columns: table => new
            {
                ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                PlayerUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                OccupiesFirstSlot = table.Column<bool>(type: "INTEGER", nullable: false),
                OccupiesSecondSlot = table.Column<bool>(type: "INTEGER", nullable: false),
                EquippedAt = table.Column<long>(type: "INTEGER", nullable: false)
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

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserClaims_UserId",
            table: "AspNetUserClaims",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserLogins_UserId",
            table: "AspNetUserLogins",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            table: "AspNetUsers",
            column: "NormalizedEmail",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            table: "AspNetUsers",
            column: "NormalizedUserName",
            unique: true);

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
            name: "ArmyPlacements");

        migrationBuilder.DropTable(
            name: "AspNetUserClaims");

        migrationBuilder.DropTable(
            name: "AspNetUserLogins");

        migrationBuilder.DropTable(
            name: "AspNetUserTokens");

        migrationBuilder.DropTable(
            name: "EquippedWeapons");

        migrationBuilder.DropTable(
            name: "ForgeStrikes");

        migrationBuilder.DropTable(
            name: "PlayerMaterials");

        migrationBuilder.DropTable(
            name: "ForgedItems");

        migrationBuilder.DropTable(
            name: "PlayerUnits");

        migrationBuilder.DropTable(
            name: "ForgeSessions");

        migrationBuilder.DropTable(
            name: "AspNetUsers");
    }
}
