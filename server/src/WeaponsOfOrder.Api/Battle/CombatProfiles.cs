using Microsoft.Extensions.Options;
using WeaponsOfOrder.Api.Content;
using WeaponsOfOrder.Combat;
using WeaponsOfOrder.Infrastructure.Gameplay;

namespace WeaponsOfOrder.Api.Battle;

/// <summary>
/// Turns a Unit and what it is holding into the final stats it fights with.
/// </summary>
/// <remarks>
/// The one place the canon's <c>Final Stat = Unit Base + Weapon + Armour</c> addition happens.
/// Doing it here rather than in the simulator is what keeps the simulator from knowing what a
/// weapon is; doing it on the server rather than in the browser is what keeps a player from
/// deciding their own Power.
/// <para>
/// There is no armour term. No armour item exists, and adding a zero for one that does not would
/// be pretending the system is further along than it is — the addition gains its third term when
/// armour is real.
/// </para>
/// </remarks>
internal sealed class CombatProfiles(IOptionsSnapshot<CombatOptions> options)
{
    private readonly CombatOptions _combat = options.Value;

    public CombatTuning Tuning => _combat.ToTuning();

    public TrainingOpponentSettings TrainingOpponent => _combat.TrainingOpponent;

    /// <summary>The final stats a training combatant fights with, its Mounted state resolved.</summary>
    /// <remarks>
    /// Through the same mapping a Unit's goes through, so retuning what Mounted is worth moves the
    /// opposition with the player's own army rather than leaving it behind.
    /// </remarks>
    public CombatantStats StatsFor(TrainingCombatantSettings combatant)
        => combatant.ToStats(_combat.MovementSpeedFor(combatant.Mounted));

    /// <summary>
    /// The final stats for a Unit with these weapons in its hands.
    /// </summary>
    /// <remarks>
    /// Canon's additive model, weapon by weapon. Both hands of a two-item loadout contribute their
    /// full Power, Critical Chance and weight, because the registry is explicit that the second
    /// slot is a full weapon slot with no off-hand penalty.
    /// </remarks>
    public CombatantStats For(UnitDefinition unit, IReadOnlyList<WeaponDefinition> weapons)
    {
        var combat = unit.Combat;
        var unarmed = _combat.Unarmed;

        var power = combat.Power + weapons.Sum(weapon => weapon.Power);
        var critical = combat.CriticalChance + weapons.Sum(weapon => weapon.CriticalChance);
        var interval = combat.AttackIntervalSeconds + weapons.Sum(weapon => _combat.IntervalFor(weapon.Weight));

        if (weapons.Count == 0)
        {
            power += unarmed.Power;
            critical += unarmed.CriticalChance;
            interval += _combat.IntervalFor(Enum.Parse<WeaponWeight>(unarmed.Weight, ignoreCase: true));
        }

        return new CombatantStats
        {
            Hp = combat.Hp,
            Power = power,

            // No armour exists, so a Unit's Defense is its own. Canon says Defense is hard to obtain
            // and comes heavily from armour, which is exactly what makes it small right now.
            Defense = combat.Defense,

            // Floored rather than clamped both ways: a slower loadout is a real choice, an
            // instantaneous one is not. The simulator applies the same floor again.
            AttackIntervalSeconds = Math.Max(interval, _combat.Tuning.MinimumAttackIntervalSeconds),

            // A probability cannot exceed certainty, however many sources add to it.
            CriticalChance = Math.Clamp(critical, 0, 1),

            Range = Reach(weapons, unarmed.Range),

            // Resolved here rather than passed along. Mounted is Unit identity; the simulator is
            // handed a number and never learns what produced it, which is what keeps the boundary
            // at final combat values.
            MovementSpeed = _combat.MovementSpeedFor(unit.Mounted),
        };
    }

    /// <summary>
    /// How far a loadout reaches.
    /// </summary>
    /// <remarks>
    /// <strong>Prototype implementation detail, not canon.</strong> The registry authors a range
    /// per weapon and says nothing about a loadout holding two weapons of different reach, because
    /// no such loadout exists yet. The longer one wins: a Unit holding something that reaches is a
    /// Unit that can use it, and the alternative — the shorter hand deciding — would make adding a
    /// weapon strictly worse.
    /// <para>
    /// Reach never comes from what a Unit is called. There are no weapon proficiency restrictions
    /// and no Unit is inherently ranged.
    /// </para>
    /// </remarks>
    private static int Reach(IReadOnlyList<WeaponDefinition> weapons, int unarmedRange)
        => weapons.Count == 0 ? unarmedRange : weapons.Max(weapon => weapon.Range);
}
