using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeaponsOfOrder.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialSchema : Migration
{
    /// <inheritdoc />
    /// <remarks>
    /// Intentionally empty. Applying it creates only EF Core's __EFMigrationsHistory
    /// table, which is what establishes and proves the migration pipeline. Identity
    /// tables arrive with Task 2 and gameplay tables with the tasks that own them.
    /// </remarks>
    protected override void Up(MigrationBuilder migrationBuilder)
    {

    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {

    }
}
