using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// Where Mounted becomes a number.
/// </summary>
/// <remarks>
/// Mounted is Unit identity and lives in content; Movement Speed is one of canon's six universal
/// combat stats and is what a battle is fought with. The translation between them happens here, in
/// the API, and these tests are what hold it here — the simulator has no way to express Mounted and
/// no way to be told about it.
/// </remarks>
public class MovementSpeedTests(BattleApiFactory factory)
    : IClassFixture<BattleApiFactory>, IAsyncLifetime
{
    /// <summary>The configured prototype mapping, mirrored from <c>appsettings.json</c>.</summary>
    private const double Foot = 1.0;
    private const double Mounted = 1.4;

    private CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => factory.InitializeAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_Unit_on_foot_resolves_the_configured_standard_speed()
    {
        using var client = await factory.SignedInAsync("speed-foot", Token);

        var army = await client.ReadArmyAsync(Token);

        Assert.False(army.Unit(PreparationApi.MeleeKey).Mounted);
        Assert.Equal(Foot, army.Unit(PreparationApi.MeleeKey).Stats.MovementSpeed, 6);
        Assert.Equal(Foot, army.Unit(PreparationApi.RangedKey).Stats.MovementSpeed, 6);
    }

    [Fact]
    public async Task A_Mounted_Unit_resolves_the_configured_faster_speed()
    {
        using var client = await factory.SignedInAsync("speed-mounted", Token);

        var mounted = (await client.ReadArmyAsync(Token)).Unit(PreparationApi.MountedKey);

        Assert.True(mounted.Mounted);
        Assert.Equal(Mounted, mounted.Stats.MovementSpeed, 6);

        // Canon's one inherent movement distinction: faster, not merely different.
        Assert.True(mounted.Stats.MovementSpeed > Foot);
    }

    /// <summary>The battle is fought with the speed the army published.</summary>
    [Fact]
    public async Task A_battle_is_fought_with_the_resolved_Movement_Speed()
    {
        using var client = await factory.SignedInAsync("speed-battle", Token);

        var army = await client.ReadArmyAsync(Token);
        await client.DeployEveryUnitAsync(army, Token);

        var result = await client.SimulateAsync(Token);

        foreach (var expected in army.Units)
        {
            var fighting = Assert.Single(result.Combatants, combatant => combatant.UnitId == expected.UnitId);

            Assert.Equal(expected.Stats.MovementSpeed, fighting.Stats.MovementSpeed, 6);
        }

        // The training opposition goes through the same mapping, so it is on the same scale rather
        // than carrying a speed of its own.
        Assert.All(
            result.Combatants.Where(combatant => combatant.Side == "opponent"),
            combatant => Assert.Contains(combatant.Stats.MovementSpeed, new[] { Foot, Mounted }));
    }

    /// <summary>
    /// Putting a weapon in a Unit's hands does not change how fast it walks.
    /// </summary>
    /// <remarks>
    /// Canon says there are no equipment movement modifiers, so a loadout that changes Power,
    /// Critical Chance, Attack Interval and reach must leave Movement Speed exactly where it was.
    /// </remarks>
    [Fact]
    public async Task An_equipped_weapon_does_not_change_Movement_Speed()
    {
        using var client = await factory.SignedInAsync("speed-armed", Token);

        var before = (await client.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey);
        var sword = await client.ForgeSwordAsync(factory, Token);

        await client.EquipAsync(before.UnitId, sword, Token);

        var after = (await client.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey);

        Assert.Equal(before.Stats.MovementSpeed, after.Stats.MovementSpeed, 6);

        // Not a vacuous assertion: the loadout did change the stats it is supposed to change.
        Assert.NotEqual(before.Stats.Power, after.Stats.Power);
    }
}

/// <summary>
/// Retuning what Mounted is worth, with Unit content untouched.
/// </summary>
/// <remarks>
/// The mapping is configuration in the API layer, so changing it must move every Unit's final
/// Movement Speed without a content edit, a schema change or a line of C#. That is the whole
/// reason the translation is not inside the simulator.
/// </remarks>
public class RetunedMovementSpeedTests(RetunedMovementApiFactory factory)
    : IClassFixture<RetunedMovementApiFactory>, IAsyncLifetime
{
    private CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => factory.InitializeAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Changing_the_mapping_changes_every_Unit_without_touching_content()
    {
        using var client = await factory.SignedInAsync("speed-retuned", Token);

        var army = await client.ReadArmyAsync(Token);

        Assert.Equal(RetunedMovementApiFactory.Foot, army.Unit(PreparationApi.MeleeKey).Stats.MovementSpeed, 6);
        Assert.Equal(RetunedMovementApiFactory.Mounted, army.Unit(PreparationApi.MountedKey).Stats.MovementSpeed, 6);

        // The definitions themselves are the ones the creator authored. Only what the API decides
        // their Mounted flag is worth has moved.
        Assert.True(army.Unit(PreparationApi.MountedKey).Mounted);
        Assert.False(army.Unit(PreparationApi.MeleeKey).Mounted);
        Assert.Equal("Mounted", army.Unit(PreparationApi.MountedKey).Name);
    }
}

/// <summary>
/// A Unit's display name has no movement meaning.
/// </summary>
/// <remarks>
/// The flags are swapped against the names here: the Unit the creator called "Ranged" is Mounted
/// and the one called "Mounted" is not. Anything reading a name would get both answers backwards.
/// </remarks>
public class MisleadingNameMovementTests(MisleadingNameApiFactory factory)
    : IClassFixture<MisleadingNameApiFactory>, IAsyncLifetime
{
    private CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => factory.InitializeAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Movement_Speed_follows_the_flag_rather_than_the_name()
    {
        using var client = await factory.SignedInAsync("speed-names", Token);

        var army = await client.ReadArmyAsync(Token);

        var namedRanged = army.Unit(PreparationApi.RangedKey);
        var namedMounted = army.Unit(PreparationApi.MountedKey);

        Assert.Equal("Ranged", namedRanged.Name);
        Assert.Equal("Mounted", namedMounted.Name);

        Assert.True(namedRanged.Stats.MovementSpeed > namedMounted.Stats.MovementSpeed);
    }
}
