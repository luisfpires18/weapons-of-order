namespace WeaponsOfOrder.Infrastructure.Gameplay;

/// <summary>
/// One hammer blow, with the heat band the server measured at the moment it landed.
/// </summary>
/// <remarks>
/// Stored rather than reduced to a running score so the finished item's quality can be
/// explained by the forging that produced it, and so a resumed session shows the player the
/// same strikes they already made.
/// <para>
/// <see cref="Ordinal"/> is unique within a session. That is what makes a double-submitted
/// strike a database conflict instead of two blows from one press.
/// </para>
/// </remarks>
public sealed class ForgeStrike
{
    public Guid Id { get; set; }

    public Guid ForgeSessionId { get; set; }

    /// <summary>1-based position in the sequence.</summary>
    public int Ordinal { get; set; }

    public HeatBand Band { get; set; }

    /// <summary>The temperature the server calculated for the instant of the blow.</summary>
    public double Temperature { get; set; }

    public DateTimeOffset StruckAt { get; set; }

    public ForgeSession? Session { get; set; }
}
