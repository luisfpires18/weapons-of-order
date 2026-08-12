using WeaponsOfOrder.Api.Forge;
using WeaponsOfOrder.Infrastructure.Gameplay;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// The forge's rules on their own, without a database or a request in the way.
/// </summary>
/// <remarks>
/// These are the assertions that keep the temperature model honest: it has to give the same
/// answer whether a player's requests arrive every 50ms or once after ten seconds, because
/// what the server actually stores is one anchor and a direction.
/// </remarks>
public sealed class ForgeRulesTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 12, 9, 0, 0, TimeSpan.Zero);

    private static readonly ForgeOptions Options = new();

    [Fact]
    public void Cold_iron_left_alone_stays_cold()
    {
        var projection = ForgeRules.Project(Session(temperature: 0, heating: false), Start.AddSeconds(30), Options);

        Assert.Equal(0, projection.Temperature);
        Assert.Equal(HeatBand.Cold, projection.Band);
        Assert.False(projection.Ruined);
    }

    [Fact]
    public void Heating_raises_the_temperature_at_the_configured_rate()
    {
        var projection = ForgeRules.Project(Session(temperature: 0, heating: true), Start.AddSeconds(2), Options);

        Assert.Equal(60, projection.Temperature, 6);
        Assert.Equal(HeatBand.Workable, projection.Band);
    }

    [Fact]
    public void Temperature_never_passes_the_top_of_the_scale()
    {
        var projection = ForgeRules.Project(Session(temperature: 0, heating: true), Start.AddSeconds(60), Options);

        Assert.Equal(Options.Heat.MaxTemperature, projection.Temperature);
    }

    [Fact]
    public void Cooling_never_falls_below_the_bottom_of_the_scale()
    {
        var projection = ForgeRules.Project(Session(temperature: 30, heating: false), Start.AddSeconds(60), Options);

        Assert.Equal(0, projection.Temperature);
    }

    /// <summary>
    /// One long step and many short ones have to agree, or a player on a slow connection
    /// would be forging a different game from a player on a fast one.
    /// </summary>
    [Fact]
    public void Projecting_in_one_step_matches_projecting_in_many()
    {
        var single = ForgeRules.Project(Session(temperature: 10, heating: true), Start.AddSeconds(4), Options);

        var session = Session(temperature: 10, heating: true);
        for (var step = 0; step < 40; step++)
        {
            var moment = Start.AddSeconds(0.1 * (step + 1));
            var stepped = ForgeRules.Project(session, moment, Options);
            session.Temperature = stepped.Temperature;
            session.BurnSeconds = stepped.BurnSeconds;
            session.TemperatureAt = moment;
        }

        Assert.Equal(single.Temperature, session.Temperature, 6);
        Assert.Equal(single.BurnSeconds, session.BurnSeconds, 6);
    }

    [Fact]
    public void Burning_time_starts_only_once_the_workpiece_reaches_the_burning_band()
    {
        // 0 to 85 at 30 a second is 2.8333s; the remainder of a 4s hold is burning.
        var projection = ForgeRules.Project(Session(temperature: 0, heating: true), Start.AddSeconds(4), Options);

        Assert.Equal(4 - (85 / 30d), projection.BurnSeconds, 6);
        Assert.Equal(HeatBand.Burning, projection.Band);
        Assert.False(projection.Ruined);
    }

    [Fact]
    public void Burning_time_stops_accruing_once_the_workpiece_cools_out_of_the_band()
    {
        // From 94, cooling at 18 a second, it is out of the burning band after half a second.
        var projection = ForgeRules.Project(Session(temperature: 94, heating: false), Start.AddSeconds(10), Options);

        Assert.Equal(0.5, projection.BurnSeconds, 6);
        Assert.Equal(HeatBand.Cold, projection.Band);
    }

    [Fact]
    public void A_workpiece_held_in_the_fire_past_the_grace_period_is_ruined()
    {
        var projection = ForgeRules.Project(Session(temperature: 0, heating: true), Start.AddSeconds(6), Options);

        Assert.True(projection.BurnSeconds >= Options.Heat.BurnGraceSeconds);
        Assert.True(projection.Ruined);
    }

    /// <summary>
    /// Burning is cumulative rather than consecutive: pulling the piece out for a moment
    /// does not undo the damage already done.
    /// </summary>
    [Fact]
    public void Burning_time_carries_across_a_pause_out_of_the_fire()
    {
        var session = Session(temperature: 90, heating: true);
        session.BurnSeconds = 2.5;

        var projection = ForgeRules.Project(session, Start.AddSeconds(0.6), Options);

        Assert.Equal(3.1, projection.BurnSeconds, 6);
        Assert.True(projection.Ruined);
    }

    [Theory]
    [InlineData(0, HeatBand.Cold)]
    [InlineData(39.9, HeatBand.Cold)]
    [InlineData(40, HeatBand.Workable)]
    [InlineData(64.9, HeatBand.Workable)]
    [InlineData(65, HeatBand.Ideal)]
    [InlineData(84.9, HeatBand.Ideal)]
    [InlineData(85, HeatBand.Burning)]
    [InlineData(100, HeatBand.Burning)]
    public void Bands_are_read_off_the_configured_boundaries(double temperature, HeatBand expected)
        => Assert.Equal(expected, ForgeRules.BandFor(temperature, Options.Heat));

    [Theory]
    // Three Ideal blows, and two Ideal with one Workable, both reach the top.
    [InlineData(Craftsmanship.Epic, HeatBand.Ideal, HeatBand.Ideal, HeatBand.Ideal)]
    [InlineData(Craftsmanship.Epic, HeatBand.Ideal, HeatBand.Ideal, HeatBand.Workable)]
    // Decent forging is Rare, including three merely workable blows.
    [InlineData(Craftsmanship.Rare, HeatBand.Ideal, HeatBand.Ideal, HeatBand.Cold)]
    [InlineData(Craftsmanship.Rare, HeatBand.Workable, HeatBand.Workable, HeatBand.Workable)]
    [InlineData(Craftsmanship.Rare, HeatBand.Ideal, HeatBand.Workable, HeatBand.Burning)]
    // Poor forging still produces a sword. Canon asks for routine forging to be forgiving.
    [InlineData(Craftsmanship.Common, HeatBand.Cold, HeatBand.Cold, HeatBand.Cold)]
    [InlineData(Craftsmanship.Common, HeatBand.Workable, HeatBand.Cold, HeatBand.Burning)]
    [InlineData(Craftsmanship.Common, HeatBand.Burning, HeatBand.Burning, HeatBand.Burning)]
    public void Craftsmanship_follows_the_strikes_that_landed(Craftsmanship expected, params HeatBand[] strikes)
        => Assert.Equal(expected, ForgeRules.CraftsmanshipFor(strikes, Options.Craftsmanship));

    /// <summary>Canon is explicit that ordinary blacksmithing has no Legendary tier.</summary>
    [Fact]
    public void No_sequence_of_perfect_strikes_produces_anything_above_epic()
    {
        var perfect = Enumerable.Repeat(HeatBand.Ideal, 20);

        Assert.Equal(Craftsmanship.Epic, ForgeRules.CraftsmanshipFor(perfect, Options.Craftsmanship));
        Assert.Equal(3, Enum.GetValues<Craftsmanship>().Length);
    }

    private static ForgeSession Session(double temperature, bool heating) => new()
    {
        Id = Guid.CreateVersion7(),
        RecipeKey = ForgeApi.SwordRecipe,
        Status = ForgeSessionStatus.Active,
        StartedAt = Start,
        Temperature = temperature,
        TemperatureAt = Start,
        IsHeating = heating,
    };
}
