namespace WeaponsOfOrder.Combat;

/// <summary>
/// The battle's only source of chance.
/// </summary>
/// <remarks>
/// Counter-based rather than a stream. Every roll is a pure hash of the seed and the four things
/// that identify the moment it belongs to — when, who, what kind of roll, and which one — so no
/// roll depends on how many rolls came before it.
/// <para>
/// That matters more than it sounds. With a sequential generator, the value a Unit gets depends
/// on the order the simulator happened to walk the roster in, and canon's simultaneous-batch
/// rule then becomes entangled with RNG consumption order: change the iteration and every crit
/// after that point changes. Here the iteration order cannot reach the dice at all.
/// </para>
/// <para>
/// The mixer is SplitMix64's finalizer, which is public-domain, two lines long, and passes the
/// statistical bar a critical-hit roll needs by a wide margin.
/// </para>
/// </remarks>
public sealed class DeterministicRandom(long seed)
{
    /// <summary>What a roll is for, so two different decisions at one moment cannot collide.</summary>
    public enum Purpose
    {
        Critical = 1,
    }

    public long Seed { get; } = seed;

    /// <summary>
    /// A value in <c>[0, 1)</c> for one specific decision.
    /// </summary>
    /// <param name="timeMilliseconds">The simulated moment the decision belongs to.</param>
    /// <param name="combatant">Who is deciding.</param>
    /// <param name="purpose">What is being decided.</param>
    /// <param name="ordinal">
    /// Which of that combatant's decisions this is — its attack count, here. Without it, a Unit
    /// attacking twice at one timestamp would roll the same number twice.
    /// </param>
    public double Next(int timeMilliseconds, CombatantId combatant, Purpose purpose, int ordinal)
    {
        var state = unchecked((ulong)Seed);

        state = Mix(state ^ 0x9E3779B97F4A7C15UL * (ulong)(uint)timeMilliseconds);
        state = Mix(state ^ 0xBF58476D1CE4E5B9UL * (ulong)(uint)Key(combatant));
        state = Mix(state ^ 0x94D049BB133111EBUL * (ulong)(uint)purpose);
        state = Mix(state ^ 0xD6E8FEB86659FD93UL * (ulong)(uint)ordinal);

        // The top 53 bits are the ones a double can hold without losing any of them, which is
        // what keeps the distribution even across the whole range.
        return (state >> 11) * (1.0 / 9007199254740992.0);
    }

    /// <summary>Whether a chance in <c>[0, 1]</c> succeeds at this moment.</summary>
    /// <remarks>
    /// A chance of zero never succeeds and a chance of one always does, both without consulting
    /// the generator — <c>Next</c> can return exactly zero, and a Unit with no Critical Chance
    /// critting occasionally would be a bug nobody would find quickly.
    /// </remarks>
    public bool Chance(
        double probability,
        int timeMilliseconds,
        CombatantId combatant,
        Purpose purpose,
        int ordinal)
    {
        if (probability <= 0)
        {
            return false;
        }

        return probability >= 1 || Next(timeMilliseconds, combatant, purpose, ordinal) < probability;
    }

    /// <summary>A combatant's identity as one number, so the two sides cannot share a key.</summary>
    private static int Key(CombatantId combatant) => ((int)combatant.Side << 16) | (combatant.Index & 0xFFFF);

    private static ulong Mix(ulong value)
    {
        unchecked
        {
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9UL;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBUL;

            return value ^ (value >> 31);
        }
    }
}
