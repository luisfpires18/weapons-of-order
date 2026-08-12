namespace WeaponsOfOrder.Infrastructure.Gameplay;

/// <summary>
/// One ordinary forge operation: the workpiece on the anvil, its heat, and the strikes
/// that have landed on it.
/// </summary>
/// <remarks>
/// The server owns every part of this. The browser never submits a temperature, a strike
/// quality or a result; it asks the server to start heating, to stop heating, or to strike,
/// and the server decides what that meant.
/// <para>
/// Heat is stored as an anchor rather than a ticking value: <see cref="Temperature"/> is
/// what the workpiece was at <see cref="TemperatureAt"/>, and <see cref="IsHeating"/> says
/// which way it has been moving since. Any later instant is a closed-form calculation from
/// those three fields, so no background job has to run and a reload cannot invent a
/// different history than the one the database already implies.
/// </para>
/// </remarks>
public sealed class ForgeSession
{
    public Guid Id { get; set; }

    /// <summary>The account that paid for this workpiece and is the only one that may touch it.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>The configured recipe being forged, for example <c>weapon.sword</c>.</summary>
    public string RecipeKey { get; set; } = string.Empty;

    public ForgeSessionStatus Status { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>The workpiece temperature as of <see cref="TemperatureAt"/>.</summary>
    public double Temperature { get; set; }

    /// <summary>The instant <see cref="Temperature"/> and <see cref="BurnSeconds"/> describe.</summary>
    public DateTimeOffset TemperatureAt { get; set; }

    /// <summary>Whether the player is holding the workpiece in the fire.</summary>
    public bool IsHeating { get; set; }

    /// <summary>
    /// Cumulative seconds spent in the Burning band as of <see cref="TemperatureAt"/>. Past
    /// the configured grace the workpiece is ruined.
    /// </summary>
    public double BurnSeconds { get; set; }

    public int StrikesTaken { get; set; }

    /// <summary>Used for the strike cooldown, which is also what stops a double-submit landing twice.</summary>
    public DateTimeOffset? LastStrikeAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>Set once, when the final strike lands. Null on an unfinished or ruined operation.</summary>
    public Craftsmanship? Craftsmanship { get; set; }

    public ICollection<ForgeStrike> Strikes { get; set; } = [];
}
