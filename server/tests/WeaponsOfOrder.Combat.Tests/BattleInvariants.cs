using Xunit;

namespace WeaponsOfOrder.Combat.Tests;

/// <summary>
/// Replays a battle's event log and checks the rules that must hold at every moment of it.
/// </summary>
/// <remarks>
/// A single assertion cannot catch a Unit stepping through an ally on the four hundredth tick of
/// a crowded fight. Replaying the log can, and it also proves the log is complete enough for a
/// client to reconstruct the battle from — which is the other half of what it is for.
/// <para>
/// Event order within a timestamp is the order the simulator applied them, so this replay reads
/// them straight through.
/// </para>
/// </remarks>
internal static class BattleInvariants
{
    public static void AssertConsistent(BattleResult result)
    {
        var field = result.Battlefield;
        var entries = result.Combatants.ToDictionary(combatant => combatant.Id);
        var positions = new Dictionary<CombatantId, Hex>();
        var occupants = new Dictionary<Hex, CombatantId>();
        var ended = false;
        var previousTime = 0;

        foreach (var moment in result.Events)
        {
            Assert.False(ended, "An event was recorded after the battle ended.");
            Assert.True(
                moment.TimeMilliseconds >= previousTime,
                $"Events ran backwards: {moment.TimeMilliseconds} after {previousTime}.");
            previousTime = moment.TimeMilliseconds;

            switch (moment)
            {
                case CombatantDeployed deployed:
                    Occupy(deployed.Id, deployed.Hex);
                    Assert.True(
                        field.IsDeploymentHexFor(entries[deployed.Id].Side, deployed.Hex),
                        $"{deployed.Id} deployed outside its own half at {deployed.Hex}.");
                    break;

                case ReserveEntered entered:
                    Assert.Equal(entries[entered.Id].ReserveEntryHex, entered.Hex);
                    Occupy(entered.Id, entered.Hex);
                    break;

                case CombatantMoved moved:
                    Assert.Equal(moved.From, positions[moved.Id]);
                    Assert.Equal(1, moved.From.DistanceTo(moved.To));
                    Assert.True(moved.To.IsOn(field), $"{moved.Id} stepped off the battlefield to {moved.To}.");
                    occupants.Remove(moved.From);
                    Occupy(moved.Id, moved.To);
                    break;

                case CombatantDied died:
                    Assert.Equal(died.Hex, positions[died.Id]);
                    occupants.Remove(died.Hex);
                    positions.Remove(died.Id);
                    break;

                case AttackResolved attack:
                    Assert.NotEqual(attack.AttackerId, attack.TargetId);
                    Assert.NotEqual(entries[attack.AttackerId].Side, entries[attack.TargetId].Side);
                    Assert.True(attack.Damage >= 1, "An attack landed for less than one damage.");
                    Assert.InRange(attack.AttackerEnergyAfter, 0, 100);
                    Assert.True(
                        positions[attack.AttackerId].DistanceTo(positions[attack.TargetId])
                            <= entries[attack.AttackerId].Stats.Range,
                        $"{attack.AttackerId} attacked out of range.");
                    break;

                case BattleEnded:
                    ended = true;
                    break;

                default:
                    throw new InvalidOperationException($"Unhandled event {moment.GetType().Name}.");
            }
        }

        Assert.True(ended, "The battle produced no end event.");
        AssertFinalStateMatches(result, positions);
        return;

        void Occupy(CombatantId id, Hex hex)
        {
            Assert.False(
                occupants.TryGetValue(hex, out var sitting),
                $"{id} entered {hex}, which {sitting} was already standing on.");

            occupants[hex] = id;
            positions[id] = hex;
        }
    }

    /// <summary>The roster the result reports has to be the one the events add up to.</summary>
    private static void AssertFinalStateMatches(BattleResult result, Dictionary<CombatantId, Hex> positions)
    {
        foreach (var combatant in result.Combatants)
        {
            switch (combatant.EndState)
            {
                case CombatantEndState.Active:
                    Assert.Equal(positions[combatant.Id], combatant.FinalHex);
                    Assert.True(combatant.FinalHp > 0, $"{combatant.Id} finished active with no HP.");
                    break;

                case CombatantEndState.Dead:
                    Assert.DoesNotContain(combatant.Id, positions.Keys);
                    Assert.Equal(0, combatant.FinalHp);
                    break;

                case CombatantEndState.Reserve:
                    Assert.DoesNotContain(combatant.Id, positions.Keys);
                    Assert.Null(combatant.FinalHex);
                    Assert.True(combatant.FinalHp > 0, $"{combatant.Id} waited in reserve with no HP.");
                    break;

                default:
                    throw new InvalidOperationException($"Unhandled end state {combatant.EndState}.");
            }
        }
    }
}
