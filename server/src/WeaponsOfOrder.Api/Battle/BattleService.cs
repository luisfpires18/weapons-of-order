using System.Security.Cryptography;
using WeaponsOfOrder.Combat;

namespace WeaponsOfOrder.Api.Battle;

/// <summary>
/// Turns the player's saved army into a battle, and the battle into something a browser can draw.
/// </summary>
/// <remarks>
/// The whole of the server's part in a battle. Everything authoritative is decided here or below
/// it: who is on the board, what they fight with, who the opposition is, what the seed is, and
/// what happened. The request that starts a battle carries nothing but a session cookie.
/// <para>
/// A battle is not persisted. It is resolved from the army and returned; battle history and
/// replay storage are systems nobody has asked for yet, and the result is reproducible from its
/// seed and snapshot if they are ever wanted.
/// </para>
/// </remarks>
internal sealed class BattleService(ArmyService army, CombatProfiles profiles)
{
    public async Task<BattleResultPayload> SimulateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var resolved = await army.ReadAsync(userId, cancellationToken);

        if (!resolved.Ready)
        {
            throw BattleProblems.EmptyArmy();
        }

        var input = new BattleInput
        {
            // Server-owned, and freshly minted for each battle. The browser has no say in it: a
            // caller who could choose the seed could roll for criticals until it liked the answer.
            Seed = NewSeed(),
            Player = PlayerArmy(resolved),
            Opponent = TrainingArmy(),
            Battlefield = resolved.Battlefield,
            Tuning = resolved.Tuning,
        };

        return ToPayload(BattleSimulator.Simulate(input));
    }

    private static long NewSeed()
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        RandomNumberGenerator.Fill(bytes);

        return BitConverter.ToInt64(bytes);
    }

    /// <summary>
    /// The player's army, with each Unit's own identifier carried through as its reference.
    /// </summary>
    /// <remarks>
    /// The reference is what lets the client match a combatant in the result back to the Unit on
    /// its roster. The simulator never reads it.
    /// </remarks>
    private static BattleArmyInput PlayerArmy(ResolvedArmy resolved) => new(
        "Your army",
        [
            .. resolved.Active.Select(member => new DeployedCombatantInput(
                new BattleCombatantInput(member.UnitId.ToString(), member.Unit.Name, member.Stats),
                member.Hex!.Value)),
        ],
        [
            .. resolved.Reserves.Select(member => new BattleCombatantInput(
                member.UnitId.ToString(),
                member.Unit.Name,
                member.Stats)),
        ]);

    /// <summary>
    /// The temporary training opposition, straight from configuration.
    /// </summary>
    /// <remarks>
    /// It is a battle harness rather than game content: no Unit definitions, no forged weapons, no
    /// account, nothing persisted, and neutral placeholder names. It exists only so the engine can
    /// be watched running, and it is replaced whole when a real opponent — a recruited defensive
    /// army, a matched player — exists.
    /// </remarks>
    private BattleArmyInput TrainingArmy()
    {
        var opponent = profiles.TrainingOpponent;

        return new BattleArmyInput(
            opponent.Name,
            [
                .. opponent.Active.Select(combatant => new DeployedCombatantInput(
                    new BattleCombatantInput(string.Empty, combatant.Name, combatant.ToStats()),
                    new Hex(combatant.Column, combatant.Row))),
            ],
            [
                .. opponent.Reserves.Select(combatant => new BattleCombatantInput(
                    string.Empty,
                    combatant.Name,
                    combatant.ToStats())),
            ]);
    }

    private static BattleResultPayload ToPayload(BattleResult result) => new(
        Lowercase(result.Outcome),
        Lowercase(result.Reason),
        result.DurationMilliseconds,
        result.Seed.ToString(),
        new BattlefieldPayload(
            result.Battlefield.Columns,
            result.Battlefield.Rows,
            result.Battlefield.HalfColumns),
        [.. result.Combatants.Select(ToPayload)],
        [.. result.Events.Select(ToPayload)]);

    private static BattleCombatantPayload ToPayload(BattleCombatant combatant) => new(
        combatant.Id.ToString(),
        Lowercase(combatant.Side),
        Guid.TryParse(combatant.Reference, out var unitId) ? unitId : null,
        combatant.Name,
        new CombatStatsPayload(
            combatant.Stats.Hp,
            combatant.Stats.Power,
            combatant.Stats.Defense,
            combatant.Stats.AttackIntervalSeconds,
            combatant.Stats.CriticalChance,
            combatant.Stats.Range,
            combatant.Stats.Mounted),
        combatant.ReserveOrder,
        ToPayload(combatant.ReserveEntryHex),
        Lowercase(combatant.EndState),
        combatant.FinalHp,
        combatant.FinalEnergy,
        ToPayload(combatant.FinalHex));

    private static BattleEventPayload ToPayload(BattleEvent moment) => moment switch
    {
        CombatantDeployed deployed => new DeployedEventPayload(
            deployed.TimeMilliseconds,
            deployed.Id.ToString(),
            ToPayload(deployed.Hex)),

        ReserveEntered entered => new ReserveEnteredEventPayload(
            entered.TimeMilliseconds,
            entered.Id.ToString(),
            ToPayload(entered.Hex)),

        CombatantMoved moved => new MovedEventPayload(
            moved.TimeMilliseconds,
            moved.Id.ToString(),
            ToPayload(moved.From),
            ToPayload(moved.To)),

        AttackResolved attack => new AttackEventPayload(
            attack.TimeMilliseconds,
            attack.AttackerId.ToString(),
            attack.TargetId.ToString(),
            Lowercase(attack.Kind),
            attack.Critical,
            attack.Damage,
            attack.TargetHpAfter,
            attack.AttackerEnergyAfter),

        CombatantDied died => new DiedEventPayload(
            died.TimeMilliseconds,
            died.Id.ToString(),
            ToPayload(died.Hex)),

        BattleEnded ended => new EndedEventPayload(
            ended.TimeMilliseconds,
            Lowercase(ended.Outcome),
            Lowercase(ended.Reason)),

        _ => throw new InvalidOperationException($"Unhandled battle event {moment.GetType().Name}."),
    };

    private static HexPayload? ToPayload(Hex? hex) => hex is { } value ? new HexPayload(value.Column, value.Row) : null;

    private static HexPayload ToPayload(Hex hex) => new(hex.Column, hex.Row);

    /// <summary>
    /// Enum names, lower-cased, so the wire contract is a stable string the client can switch on
    /// rather than an ordinal that moves when the enum is edited.
    /// </summary>
    private static string Lowercase<T>(T value)
        where T : struct, Enum
        => value.ToString().ToLowerInvariant();
}
