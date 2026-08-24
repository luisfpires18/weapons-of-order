namespace WeaponsOfOrder.Combat;

/// <summary>
/// The v1 damage pipeline, exactly in canon's order.
/// </summary>
/// <remarks>
/// Power to raw damage, attack coefficient, critical, Defense mitigation, round to the nearest
/// whole number, then a floor of one on a hit that lands. Kept as pure static functions so the
/// arithmetic can be asserted on directly rather than only observed through a battle.
/// </remarks>
public static class DamageMath
{
    /// <summary>The share of damage that survives a given Defense.</summary>
    /// <remarks>
    /// Canon expresses mitigation as <c>Defense / (Defense + K)</c> reduced; this is the
    /// remaining multiplier, <c>K / (Defense + K)</c>, which is the same curve read from the
    /// other end and one operation shorter.
    /// </remarks>
    public static double DamageMultiplier(int defense, CombatTuning tuning)
    {
        var effective = Math.Max(0, defense);

        return tuning.DefenseConstant / (effective + tuning.DefenseConstant);
    }

    /// <summary>The fraction of incoming damage a given Defense removes.</summary>
    public static double Reduction(int defense, CombatTuning tuning)
        => 1 - DamageMultiplier(defense, tuning);

    /// <summary>Raw auto-attack damage before coefficients, criticals and Defense.</summary>
    public static double RawAuto(int power, CombatTuning tuning)
        => Math.Max(0, power) * tuning.PowerScale;

    /// <summary>
    /// What one attack takes off its target.
    /// </summary>
    /// <remarks>
    /// The minimum of one applies to a hit that lands, which every attack here is — the game has
    /// no miss chance. A Unit with no Power still chips.
    /// </remarks>
    public static int Damage(int power, AttackKind kind, bool critical, int defense, CombatTuning tuning)
    {
        var raw = RawAuto(power, tuning)
            * (kind == AttackKind.Heavy ? tuning.HeavyCoefficient : tuning.NormalCoefficient);

        if (critical)
        {
            raw *= tuning.CriticalMultiplier;
        }

        var mitigated = raw * DamageMultiplier(defense, tuning);

        // Nearest whole number, halves away from zero. Banker's rounding is .NET's default and
        // would make 12.5 and 13.5 both land on an even number, which is not what "round to the
        // nearest whole number" means to anyone reading the canon.
        var rounded = (int)Math.Round(mitigated, MidpointRounding.AwayFromZero);

        return Math.Max(tuning.MinimumDamage, rounded);
    }

    /// <summary>Energy after a successful attack: reset by a Heavy, topped up by an auto, never over the cap.</summary>
    public static int EnergyAfterAttack(int energy, AttackKind kind, CombatTuning tuning)
        => kind == AttackKind.Heavy
            ? 0
            : Math.Min(tuning.MaximumEnergy, energy + tuning.EnergyPerAttack);

    /// <summary>Whether a Unit at this Energy performs its 100-Energy attack instead of an auto.</summary>
    public static AttackKind KindFor(int energy, CombatTuning tuning)
        => energy >= tuning.MaximumEnergy ? AttackKind.Heavy : AttackKind.Normal;
}
