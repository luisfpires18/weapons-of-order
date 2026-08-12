using WeaponsOfOrder.Infrastructure.Gameplay;

namespace WeaponsOfOrder.Api.Forge;

/// <summary>
/// What the workpiece is at a given instant, derived rather than stored.
/// </summary>
internal readonly record struct HeatProjection(
    double Temperature,
    double BurnSeconds,
    HeatBand Band,
    bool Ruined);

/// <summary>
/// The forge's rules, as pure functions of stored state, elapsed time and configuration.
/// </summary>
/// <remarks>
/// Nothing here reads a clock, touches the database or knows about a request. The server
/// decides what a strike was worth by calling these with its own timestamp, which is the
/// whole reason the client cannot claim a heat band or a craftsmanship: there is no input
/// path from the browser into any of it.
/// <para>
/// Temperature is not simulated tick by tick. A session records what the workpiece was at
/// one instant and which way it has been moving, and every later question is answered in
/// closed form. That makes the model exact under any request timing, cheap to resume after
/// a reload, and testable without a test ever waiting on real time.
/// </para>
/// </remarks>
internal static class ForgeRules
{
    /// <summary>The state of a session's workpiece at <paramref name="now"/>.</summary>
    public static HeatProjection Project(ForgeSession session, DateTimeOffset now, ForgeOptions options)
    {
        var elapsed = (now - session.TemperatureAt).TotalSeconds;
        var heat = options.Heat;

        var temperature = session.Temperature;
        var burn = session.BurnSeconds;

        if (elapsed > 0)
        {
            temperature = session.IsHeating
                ? Math.Min(heat.MaxTemperature, session.Temperature + (heat.HeatRatePerSecond * elapsed))
                : Math.Max(0, session.Temperature - (heat.CoolRatePerSecond * elapsed));

            burn += BurnedSeconds(session.Temperature, session.IsHeating, elapsed, heat);
        }

        return new HeatProjection(
            temperature,
            burn,
            BandFor(temperature, heat),
            burn >= heat.BurnGraceSeconds);
    }

    /// <summary>
    /// How long a workpiece starting at <paramref name="from"/> spends in the Burning band
    /// over the next <paramref name="seconds"/>.
    /// </summary>
    /// <remarks>
    /// Closed form rather than sampled: temperature moves linearly and crosses the burning
    /// threshold at most once per interval, so the crossing can simply be solved for. A
    /// sampled version would let a player who timed their requests badly — or well — be
    /// charged for burn that did not happen.
    /// </remarks>
    private static double BurnedSeconds(double from, bool heating, double seconds, HeatSettings heat)
    {
        var threshold = heat.BurningFrom;

        if (heating)
        {
            // Rising, so once it is burning it stays burning for the rest of the interval.
            if (from >= threshold)
            {
                return seconds;
            }

            if (heat.HeatRatePerSecond <= 0)
            {
                return 0;
            }

            var untilBurning = (threshold - from) / heat.HeatRatePerSecond;
            return Math.Max(0, seconds - untilBurning);
        }

        // Cooling, so any burning happens at the start of the interval and then stops.
        if (from <= threshold)
        {
            return 0;
        }

        if (heat.CoolRatePerSecond <= 0)
        {
            return seconds;
        }

        return Math.Min(seconds, (from - threshold) / heat.CoolRatePerSecond);
    }

    public static HeatBand BandFor(double temperature, HeatSettings heat) => temperature switch
    {
        _ when temperature >= heat.BurningFrom => HeatBand.Burning,
        _ when temperature >= heat.IdealFrom => HeatBand.Ideal,
        _ when temperature >= heat.WorkableFrom => HeatBand.Workable,
        _ => HeatBand.Cold,
    };

    /// <summary>
    /// The craftsmanship a completed sequence of strikes earned.
    /// </summary>
    public static Craftsmanship CraftsmanshipFor(IEnumerable<HeatBand> strikes, CraftsmanshipSettings settings)
    {
        var score = strikes.Sum(settings.PointsFor);

        return score >= settings.EpicScore
            ? Craftsmanship.Epic
            : score >= settings.RareScore
                ? Craftsmanship.Rare
                : Craftsmanship.Common;
    }
}
