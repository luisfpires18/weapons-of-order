using Microsoft.Extensions.Options;
using WeaponsOfOrder.Combat;
using WeaponsOfOrder.Infrastructure.Gameplay;

namespace WeaponsOfOrder.Api.Battle;

/// <summary>
/// Refuses to start when the battle is tuned into a state it cannot run in.
/// </summary>
/// <remarks>
/// These values are meant to be edited, which is exactly why they are checked at startup. A tick
/// of zero would not throw; it would make the combat clock stand still, and the first person to
/// notice would be whoever was waiting for a battle that never came back.
/// </remarks>
internal sealed class CombatOptionsValidator : IValidateOptions<CombatOptions>
{
    public ValidateOptionsResult Validate(string? name, CombatOptions options)
    {
        var failures = new List<string>();

        failures.AddRange(TuningFailures(options.Tuning));
        failures.AddRange(MovementSpeedFailures(options.MovementSpeed));
        failures.AddRange(UnarmedFailures(options.Unarmed));
        failures.AddRange(OpponentFailures(options.TrainingOpponent, options.Tuning));

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static IEnumerable<string> TuningFailures(CombatTuningSettings tuning)
    {
        var section = $"'{CombatOptions.SectionName}:{nameof(CombatOptions.Tuning)}'";

        if (tuning.TickMilliseconds < 1)
        {
            yield return
                $"{section} TickMilliseconds must be at least 1. It is how far the combat clock "
                + "advances each step, and a step of nothing is a clock that has stopped.";
        }

        if (tuning.PowerScale <= 0)
        {
            yield return $"{section} PowerScale must be greater than zero, or Power does nothing.";
        }

        if (tuning.DefenseConstant <= 0)
        {
            yield return
                $"{section} DefenseConstant must be greater than zero. It is the denominator of the "
                + "mitigation curve.";
        }

        if (tuning.HeavyCoefficient <= 0 || tuning.NormalCoefficient <= 0 || tuning.CriticalMultiplier <= 0)
        {
            yield return $"{section} attack coefficients and the critical multiplier must be greater than zero.";
        }

        if (tuning.MinimumDamage < 1)
        {
            yield return $"{section} MinimumDamage must be at least 1: a hit that lands takes something off.";
        }

        if (tuning.MaximumEnergy < 1 || tuning.EnergyPerAttack < 1)
        {
            yield return
                $"{section} MaximumEnergy and EnergyPerAttack must both be positive, or the Heavy attack "
                + "is unreachable.";
        }

        if (tuning.BaseMovementSecondsPerHex <= 0)
        {
            yield return
                $"{section} BaseMovementSecondsPerHex must be greater than zero. It is how long a hex "
                + "takes at a Movement Speed of 1, and a step of no duration is not a fast Unit.";
        }

        if (tuning.MinimumAttackIntervalSeconds <= 0)
        {
            yield return
                $"{section} MinimumAttackIntervalSeconds must be greater than zero. It is the floor that "
                + "stops a loadout stacking its way to attacking every tick.";
        }

        if (tuning.ActiveLimit < 1 || tuning.ReserveLimit < 0 || tuning.ArmyLimit < 1)
        {
            yield return $"{section} ActiveLimit and ArmyLimit must be positive and ReserveLimit cannot be negative.";
        }
        else if (tuning.ActiveLimit > tuning.ArmyLimit)
        {
            yield return
                $"{section} ActiveLimit {tuning.ActiveLimit} is larger than ArmyLimit {tuning.ArmyLimit}, "
                + "so an army could not field the Units it is allowed to bring.";
        }

        if (tuning.ReserveEntryDelaySeconds <= 0)
        {
            yield return
                $"{section} ReserveEntryDelaySeconds must be greater than zero. Canon asks for a short "
                + "delay before a reserve attempts to enter; without one it teleports in on the instant "
                + "a Unit dies.";
        }

        if (tuning.MaximumDurationSeconds <= 0 || tuning.NoProgressSeconds <= 0)
        {
            yield return
                $"{section} MaximumDurationSeconds and NoProgressSeconds must both be greater than zero. "
                + "They are the guards that make a battle finite.";
        }
        else if (tuning.NoProgressSeconds > tuning.MaximumDurationSeconds)
        {
            yield return
                $"{section} NoProgressSeconds {tuning.NoProgressSeconds} is longer than "
                + $"MaximumDurationSeconds {tuning.MaximumDurationSeconds}, so the no-progress window can "
                + "never fire.";
        }
    }

    /// <summary>
    /// The Mounted-to-Movement-Speed mapping has to describe movement that can happen.
    /// </summary>
    /// <remarks>
    /// A speed of zero or less would divide the base duration into an infinite or negative step,
    /// and the simulator refuses such a combatant outright — better to say so at startup, naming
    /// the setting, than to have every battle fail.
    /// </remarks>
    private static IEnumerable<string> MovementSpeedFailures(MovementSpeedSettings speeds)
    {
        var section = $"'{CombatOptions.SectionName}:{nameof(CombatOptions.MovementSpeed)}'";

        if (speeds.Foot <= 0 || speeds.Mounted <= 0)
        {
            yield return
                $"{section} Foot and Mounted must both be greater than zero. Movement Speed is a "
                + "multiple of standard movement, not a duration.";
        }
        else if (speeds.Mounted < speeds.Foot)
        {
            yield return
                $"{section} Mounted {speeds.Mounted} is slower than Foot {speeds.Foot}. Canon's one "
                + "inherent movement distinction is that Mounted Units are faster.";
        }
    }

    private static IEnumerable<string> UnarmedFailures(UnarmedSettings unarmed)
    {
        var section = $"'{CombatOptions.SectionName}:{nameof(CombatOptions.Unarmed)}'";

        if (unarmed.Range < 1)
        {
            yield return $"{section} Range must be at least 1 hex, or a Unit holding nothing could never attack.";
        }

        if (unarmed.Power < 0)
        {
            yield return $"{section} Power cannot be negative.";
        }

        if (unarmed.CriticalChance is < 0 or > 1)
        {
            yield return $"{section} CriticalChance is a probability, from 0 to 1.";
        }

        if (!Enum.TryParse<WeaponWeight>(unarmed.Weight, ignoreCase: true, out _))
        {
            yield return
                $"{section} Weight '{unarmed.Weight}' must be one of: "
                + $"{string.Join(", ", Enum.GetNames<WeaponWeight>())}.";
        }
    }

    /// <summary>
    /// The training opposition has to be an army the simulator would accept.
    /// </summary>
    /// <remarks>
    /// It is a harness rather than content, but it is still the other half of every battle the
    /// creator watches. A duplicate hex or a Unit deployed in the player's half would surface as a
    /// failed battle rather than as the configuration mistake it is.
    /// </remarks>
    private static IEnumerable<string> OpponentFailures(TrainingOpponentSettings opponent, CombatTuningSettings tuning)
    {
        var section = $"'{CombatOptions.SectionName}:{nameof(CombatOptions.TrainingOpponent)}'";
        var field = Battlefield.Canonical;

        if (opponent.Active.Count == 0)
        {
            yield return $"{section} needs at least one active combatant, or there is nobody to fight.";
        }

        if (opponent.Active.Count > tuning.ActiveLimit)
        {
            yield return $"{section} deploys {opponent.Active.Count} combatants and the active limit is {tuning.ActiveLimit}.";
        }

        if (opponent.Reserves.Count > tuning.ReserveLimit)
        {
            yield return $"{section} holds {opponent.Reserves.Count} reserves and the reserve limit is {tuning.ReserveLimit}.";
        }

        if (opponent.Active.Count + opponent.Reserves.Count > tuning.ArmyLimit)
        {
            yield return $"{section} brings more combatants than the army limit of {tuning.ArmyLimit}.";
        }

        var taken = new HashSet<Hex>();

        foreach (var (combatant, index) in opponent.Active.Select((combatant, index) => (combatant, index)))
        {
            var hex = new Hex(combatant.Column, combatant.Row);
            var where = $"{section}:Active[{index}]";

            if (!field.IsDeploymentHexFor(BattleSide.Opponent, hex))
            {
                yield return $"{where} stands at {hex}, which is not in the opponent's deployment half.";
            }
            else if (!taken.Add(hex))
            {
                yield return $"{where} stands at {hex}, where another combatant already is.";
            }
        }

        foreach (var (combatant, index) in opponent.Active.Concat(opponent.Reserves).Select((combatant, index) => (combatant, index)))
        {
            var where = $"{section} combatant {index}";

            if (string.IsNullOrWhiteSpace(combatant.Name))
            {
                yield return $"{where} needs a Name.";
            }

            if (combatant.Hp < 1)
            {
                yield return $"{where} must start the battle alive.";
            }

            if (combatant.Range < 1)
            {
                yield return $"{where} must have a Range of at least 1 hex.";
            }

            if (combatant.AttackIntervalSeconds <= 0)
            {
                yield return $"{where} must have an AttackIntervalSeconds greater than zero.";
            }

            if (combatant.CriticalChance is < 0 or > 1)
            {
                yield return $"{where} has a CriticalChance outside 0 to 1.";
            }
        }
    }
}
