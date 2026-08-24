using WeaponsOfOrder.Combat.Internal;

namespace WeaponsOfOrder.Combat;

/// <summary>
/// Resolves one battle, completely and deterministically.
/// </summary>
/// <remarks>
/// The whole of the game's combat authority. It reads nothing but its <see cref="BattleInput"/>
/// — no clock, no ambient randomness, no configuration it was not handed — so the same input
/// always produces the same result, event for event.
/// <para>
/// The simulated clock advances in fixed whole-millisecond steps. Everything scheduled for one
/// step happens at that one timestamp, and canon's rule for a timestamp is strict: eligibility
/// and damage are both decided from the state immediately <em>before</em> it, so a Unit killed
/// at time T still lands the attack it had already committed for time T. That is what makes a
/// mutual last kill a Draw rather than a race between two list positions.
/// </para>
/// </remarks>
public static class BattleSimulator
{
    /// <summary>Resolves the battle described by <paramref name="input"/>.</summary>
    /// <exception cref="InvalidBattleInputException">The armies described could not fight.</exception>
    public static BattleResult Simulate(BattleInput input) => new Simulation(input).Run();
}

/// <summary>One battle in progress. Created, run once, and discarded.</summary>
internal sealed class Simulation
{
    private static readonly BattleSide[] Sides = [BattleSide.Player, BattleSide.Opponent];

    private readonly BattleInput _input;
    private readonly Battlefield _field;
    private readonly CombatTuning _tuning;
    private readonly DeterministicRandom _random;

    private readonly List<Combatant> _combatants = [];
    private readonly HexMap<Combatant> _occupants;
    private readonly List<BattleEvent> _events = [];

    private int _now;
    private int _lastProgressAt;

    public Simulation(BattleInput input)
    {
        _input = input;
        _field = input.Battlefield;
        _tuning = input.Tuning;
        _random = new DeterministicRandom(input.Seed);
        _occupants = new HexMap<Combatant>(_field);

        Validate();
        Muster();
    }

    public BattleResult Run()
    {
        while (true)
        {
            if (Frame() is { } finish)
            {
                return Finish(finish.Outcome, finish.Reason);
            }

            // The one place the clock moves. Every guard below is measured against it, so the
            // hard duration cap is reached in a bounded number of iterations no matter what the
            // armies do — there is no path through a frame that leaves time standing still.
            _now += _tuning.TickMilliseconds;
        }
    }

    /// <summary>
    /// One timestamp, in the order canon fixes it.
    /// </summary>
    /// <remarks>
    /// Commit from the pre-timestamp state, apply together, resolve deaths, decide the ordinary
    /// result, and only then let the survivors move and the reinforcements arrive. The guards go
    /// last so that a reserve entering at this timestamp counts as the progress it is.
    /// </remarks>
    private (BattleOutcome Outcome, BattleEndReason Reason)? Frame()
    {
        var attacked = ResolveAttacks();

        ResolveDeaths();

        if (OrdinaryResult() is { } decided)
        {
            return decided;
        }

        ResolveMovement(attacked);
        ResolveReserveEntries();
        ScheduleReserveEntries();

        return Guards();
    }

    /// <summary>
    /// Every attack due at this timestamp, decided from the state before it and applied together.
    /// </summary>
    /// <returns>Who attacked, so they do not also move this timestamp.</returns>
    private HashSet<CombatantId> ResolveAttacks()
    {
        // Committed first, applied second, and deliberately not fused into one loop. Deciding and
        // applying in the same pass would let the first attacker's damage change what the second
        // attacker sees — which target is alive, how much HP it has left — and that is exactly
        // the first-strike advantage canon forbids a same-timestamp attacker from having.
        var committed = new List<(Combatant Attacker, Combatant Target, AttackKind Kind, bool Critical, int Damage)>();

        foreach (var attacker in _combatants)
        {
            if (!attacker.OnBoard || attacker.AttackReadyAt > _now)
            {
                continue;
            }

            var target = SelectTarget(attacker);

            if (target is null || Distance(attacker, target) > attacker.Stats.Range)
            {
                continue;
            }

            var kind = DamageMath.KindFor(attacker.Energy, _tuning);
            var critical = _random.Chance(
                attacker.Stats.CriticalChance,
                _now,
                attacker.Id,
                DeterministicRandom.Purpose.Critical,
                attacker.AttacksMade + 1);

            var damage = DamageMath.Damage(attacker.Stats.Power, kind, critical, target.Stats.Defense, _tuning);

            committed.Add((attacker, target, kind, critical, damage));
        }

        var attackedThisFrame = new HashSet<CombatantId>();

        foreach (var (attacker, target, kind, critical, damage) in committed)
        {
            target.Hp -= damage;
            attacker.Energy = DamageMath.EnergyAfterAttack(attacker.Energy, kind, _tuning);
            attacker.AttackReadyAt = _now + attacker.AttackIntervalMilliseconds;
            attacker.AttacksMade++;
            attackedThisFrame.Add(attacker.Id);

            _events.Add(new AttackResolved(
                _now,
                attacker.Id,
                target.Id,
                kind,
                critical,
                damage,
                Math.Max(0, target.Hp),
                attacker.Energy));

            // An HP change is progress, which is what keeps two Units trading blows from tripping
            // the no-progress guard while the battle is plainly still a battle.
            MadeProgress();
        }

        return attackedThisFrame;
    }

    /// <summary>Removes whoever the timestamp's whole batch killed, after all of it has landed.</summary>
    private void ResolveDeaths()
    {
        foreach (var combatant in _combatants)
        {
            if (!combatant.OnBoard || combatant.Hp > 0)
            {
                continue;
            }

            combatant.Hp = 0;
            combatant.State = CombatantState.Dead;

            var hex = combatant.Position!.Value;
            _occupants[hex] = null;

            // The position is kept rather than cleared: it is where the Unit fell, which is what a
            // playback client needs in order to leave a body behind for a moment.
            _events.Add(new CombatantDied(_now, combatant.Id, hex));
            MadeProgress();
        }
    }

    /// <summary>
    /// The ordinary victory or defeat, if this timestamp produced one.
    /// </summary>
    /// <remarks>
    /// Liveness, not board occupancy. An army with nothing on the battlefield and a living reserve
    /// waiting behind a blocked entry hex is not defeated, and a Unit that cannot get in is not
    /// dead.
    /// </remarks>
    private (BattleOutcome Outcome, BattleEndReason Reason)? OrdinaryResult()
    {
        var player = _combatants.Any(combatant => combatant.Side == BattleSide.Player && combatant.Alive);
        var opponent = _combatants.Any(combatant => combatant.Side == BattleSide.Opponent && combatant.Alive);

        return (player, opponent) switch
        {
            (false, false) => (BattleOutcome.Draw, BattleEndReason.MutualElimination),
            (true, false) => (BattleOutcome.PlayerVictory, BattleEndReason.Elimination),
            (false, true) => (BattleOutcome.OpponentVictory, BattleEndReason.Elimination),
            _ => null,
        };
    }

    /// <summary>
    /// One step each, for the Units with nothing in reach to hit.
    /// </summary>
    /// <remarks>
    /// A Unit that attacked this timestamp stays put, and so does one whose target is already in
    /// range but whose next attack is not due — waiting in position is what an autobattler should
    /// look like, not shuffling.
    /// <para>
    /// Movement is applied one Unit at a time in the roster's stable order, so two Units cannot
    /// both step into one hex: the second finds it occupied and routes around. Deliberately unlike
    /// attacks, because a step is not an effect resolved against another Unit, and body blocking is
    /// a mechanic canon asks for rather than an accident to design out.
    /// </para>
    /// </remarks>
    private void ResolveMovement(HashSet<CombatantId> attacked)
    {
        foreach (var combatant in _combatants)
        {
            if (!combatant.OnBoard || combatant.MoveReadyAt > _now || attacked.Contains(combatant.Id))
            {
                continue;
            }

            var reach = Reach(combatant);
            var target = SelectTarget(combatant, reach);

            if (target is null || Distance(combatant, target) <= combatant.Stats.Range)
            {
                continue;
            }

            // Spent whether or not the step lands. A Unit that is body-blocked has still used the
            // time trying, and retrying every tick would be free frantic searching.
            combatant.MoveReadyAt = _now + combatant.MovementIntervalMilliseconds;

            if (StepTowards(combatant, target, reach) is not { } step)
            {
                continue;
            }

            var from = combatant.Position!.Value;
            _occupants[from] = null;
            _occupants[step] = combatant;
            combatant.Position = step;

            // Deliberately not progress. Canon lists movement, retargeting and path attempts among
            // the things that do not reset the no-progress window, which is what makes a permanent
            // stand-off end rather than pace until the hard cap.
            _events.Add(new CombatantMoved(_now, combatant.Id, from, step));
        }
    }

    /// <summary>Reserves whose entry attempt is due at this timestamp.</summary>
    private void ResolveReserveEntries()
    {
        foreach (var combatant in _combatants)
        {
            if (combatant.State != CombatantState.Reserve || combatant.ReserveAttemptAt != _now)
            {
                continue;
            }

            var entry = combatant.ReserveEntryHex!.Value;
            var slotOpen = ActiveCount(combatant.Side) < _tuning.ActiveLimit;

            if (!slotOpen || _occupants[entry] is not null)
            {
                // No fallback hex, ever. Canon is explicit that a blocked reserve waits alive rather
                // than finding somewhere else to appear, and a failed attempt is not progress — so a
                // permanently blocked entry is precisely what the no-progress guard is for.
                combatant.ReserveAttemptAt = _now + _tuning.ReserveEntryDelayMilliseconds;
                continue;
            }

            combatant.ReserveAttemptAt = null;
            combatant.State = CombatantState.Active;
            combatant.Position = entry;
            combatant.AttackReadyAt = _now;
            combatant.MoveReadyAt = _now;
            _occupants[entry] = combatant;

            _events.Add(new ReserveEntered(_now, combatant.Id, entry));
            MadeProgress();
        }
    }

    /// <summary>
    /// Calls up as many reserves as there are open active slots.
    /// </summary>
    /// <remarks>
    /// In queue order, and only for reserves not already called. A reserve whose attempt keeps
    /// failing holds its place in the queue; the reserve behind it is called only when a second
    /// slot is open, which is the strategic uncertainty canon describes rather than a bug.
    /// </remarks>
    private void ScheduleReserveEntries()
    {
        foreach (var side in Sides)
        {
            var open = _tuning.ActiveLimit - ActiveCount(side);

            if (open <= 0)
            {
                continue;
            }

            var pending = _combatants
                .Where(combatant => combatant.Side == side && combatant.State == CombatantState.Reserve)
                .OrderBy(combatant => combatant.ReserveOrder)
                .ToList();

            var called = pending.Count(combatant => combatant.ReserveAttemptAt is not null);

            foreach (var combatant in pending)
            {
                if (called >= open)
                {
                    break;
                }

                if (combatant.ReserveAttemptAt is not null)
                {
                    continue;
                }

                combatant.ReserveAttemptAt = _now + _tuning.ReserveEntryDelayMilliseconds;
                called++;
            }
        }
    }

    /// <summary>The finite guards, evaluated once the timestamp is otherwise complete.</summary>
    private (BattleOutcome Outcome, BattleEndReason Reason)? Guards()
    {
        if (_now - _lastProgressAt >= _tuning.NoProgressMilliseconds)
        {
            return (BattleOutcome.Draw, BattleEndReason.NoProgress);
        }

        // The hard cap applies even when the no-progress window keeps resetting, so a battle that
        // cycles productively forever still ends.
        return _now >= _tuning.MaximumDurationMilliseconds
            ? (BattleOutcome.Draw, BattleEndReason.MaximumDuration)
            : null;
    }

    /// <summary>
    /// The enemy this Unit fights, by canon's priority.
    /// </summary>
    /// <remarks>
    /// Closest by hex distance; then the lower final Defense, as the mechanical expression of being
    /// the less armoured target; then the roster's own stable order, which canon expressly allows as
    /// the last resort and which is why a battle replays identically.
    /// <para>
    /// An enemy counts only if this Unit could actually fight it: already within range, or with some
    /// hex it can walk to from which it would be. That is what skips a target sealed behind a wall of
    /// bodies rather than letting a melee Unit stare at it forever, and range 1 and range 3 go
    /// through the same expression — nothing here reads a Unit's label.
    /// </para>
    /// </remarks>
    private Combatant? SelectTarget(Combatant attacker, ReachMap? reach = null)
    {
        Combatant? best = null;
        var bestDistance = int.MaxValue;

        foreach (var enemy in _combatants)
        {
            if (enemy.Side == attacker.Side || !enemy.OnBoard)
            {
                continue;
            }

            var distance = Distance(attacker, enemy);

            if (distance > bestDistance)
            {
                continue;
            }

            if (distance > attacker.Stats.Range)
            {
                // Only now is the search worth running: an enemy already within range is reachable
                // by definition, and in a melee scrum that is every enemy that matters.
                reach ??= Reach(attacker);

                if (!CanCloseOn(attacker, enemy, reach))
                {
                    continue;
                }
            }

            if (best is null || distance < bestDistance || Prefer(enemy, best))
            {
                best = enemy;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Whether <paramref name="candidate"/> beats <paramref name="incumbent"/> at equal distance.</summary>
    private static bool Prefer(Combatant candidate, Combatant incumbent)
        => candidate.Stats.Defense != incumbent.Stats.Defense
            ? candidate.Stats.Defense < incumbent.Stats.Defense
            : candidate.Id.Index < incumbent.Id.Index;

    /// <summary>Whether some hex this Unit can walk to would put <paramref name="enemy"/> in range.</summary>
    private static bool CanCloseOn(Combatant attacker, Combatant enemy, ReachMap reach)
    {
        var enemyHex = enemy.Position!.Value;

        foreach (var (hex, _) in reach.Reachable)
        {
            if (hex.DistanceTo(enemyHex) <= attacker.Stats.Range)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The one adjacent hex to take towards a position this Unit could attack from.</summary>
    /// <remarks>
    /// Among the hexes that would put the target in range, the fewest steps wins; then the one
    /// closest to the target, so a Unit closes rather than circling; then the board's own hex order,
    /// because canon says equally short routes need no authored preference and this one only has to
    /// be the same every time.
    /// </remarks>
    private static Hex? StepTowards(Combatant attacker, Combatant target, ReachMap reach)
    {
        var targetHex = target.Position!.Value;

        Hex? destination = null;
        var bestSteps = int.MaxValue;
        var bestDistance = int.MaxValue;

        foreach (var (hex, steps) in reach.Reachable)
        {
            var distance = hex.DistanceTo(targetHex);

            if (distance > attacker.Stats.Range)
            {
                continue;
            }

            if (steps < bestSteps || (steps == bestSteps && distance < bestDistance))
            {
                destination = hex;
                bestSteps = steps;
                bestDistance = distance;
            }
        }

        return destination is { } goal ? reach.FirstStepTowards(goal) : null;
    }

    private ReachMap Reach(Combatant combatant)
        => ReachMap.From(_field, combatant.Position!.Value, hex => _occupants[hex] is not null);

    private static int Distance(Combatant one, Combatant other)
        => one.Position!.Value.DistanceTo(other.Position!.Value);

    private int ActiveCount(BattleSide side)
        => _combatants.Count(combatant => combatant.Side == side && combatant.OnBoard);

    private void MadeProgress() => _lastProgressAt = _now;

    /// <summary>Seals the battle: the end event, then the roster as it finished.</summary>
    private BattleResult Finish(BattleOutcome outcome, BattleEndReason reason)
    {
        _events.Add(new BattleEnded(_now, outcome, reason));

        return new BattleResult
        {
            Outcome = outcome,
            Reason = reason,
            DurationMilliseconds = _now,
            Seed = _input.Seed,
            Battlefield = _field,
            Combatants =
            [
                .. _combatants.Select(combatant => new BattleCombatant
                {
                    Id = combatant.Id,
                    Side = combatant.Side,
                    Reference = combatant.Reference,
                    Name = combatant.Name,
                    Stats = combatant.Stats,
                    ReserveOrder = combatant.ReserveOrder,
                    ReserveEntryHex = combatant.ReserveEntryHex,
                    EndState = combatant.State switch
                    {
                        CombatantState.Active => CombatantEndState.Active,
                        CombatantState.Reserve => CombatantEndState.Reserve,
                        _ => CombatantEndState.Dead,
                    },
                    FinalHp = Math.Max(0, combatant.Hp),
                    FinalEnergy = combatant.Energy,
                    FinalHex = combatant.Position,
                }),
            ],
            Events = _events,
        };
    }

    /// <summary>Builds both armies and puts the starting Units on the board.</summary>
    private void Muster()
    {
        foreach (var side in Sides)
        {
            var army = _input.Army(side);
            var index = 0;

            foreach (var deployed in army.Active)
            {
                var combatant = Build(side, index++, deployed.Combatant, null);
                combatant.State = CombatantState.Active;
                combatant.Position = deployed.Hex;
                _occupants[deployed.Hex] = combatant;

                _combatants.Add(combatant);
                _events.Add(new CombatantDeployed(0, combatant.Id, deployed.Hex));
            }

            for (var queue = 0; queue < army.Reserves.Count; queue++)
            {
                var combatant = Build(side, index++, army.Reserves[queue], queue);
                combatant.State = CombatantState.Reserve;

                _combatants.Add(combatant);
            }
        }
    }

    private Combatant Build(BattleSide side, int index, BattleCombatantInput input, int? reserveOrder)
        => new()
        {
            Id = new CombatantId(side, index),
            Side = side,
            Reference = input.Reference,
            Name = input.Name,
            Stats = input.Stats,
            ReserveOrder = reserveOrder,
            ReserveEntryHex = reserveOrder is { } queue ? _field.ReserveEntryHex(side, queue) : null,
            AttackIntervalMilliseconds = _tuning.AttackIntervalMilliseconds(input.Stats.AttackIntervalSeconds),
            MovementIntervalMilliseconds = _tuning.MovementIntervalMilliseconds(input.Stats.Mounted),
            Hp = input.Stats.Hp,

            // Both ready at the opening bell. A Unit's first attack has no wind-up and its first
            // step is immediate; an interval is the time between actions, not before the first one.
            AttackReadyAt = 0,
            MoveReadyAt = 0,
        };

    /// <summary>Refuses a battle that could not legally be fought.</summary>
    /// <remarks>
    /// Every one of these is a caller's bug rather than a player's mistake — the API validates a
    /// deployment against the same limits long before it reaches here — so they throw instead of
    /// resolving to an outcome. An authoritative result computed from a nonsense army would be
    /// worse than no result at all.
    /// </remarks>
    private void Validate()
    {
        foreach (var side in Sides)
        {
            var army = _input.Army(side);

            if (army.Active.Count == 0 && army.Reserves.Count == 0)
            {
                throw new InvalidBattleInputException($"The {side} army has no Units.");
            }

            if (army.Active.Count > _tuning.ActiveLimit)
            {
                throw new InvalidBattleInputException(
                    $"The {side} army deploys {army.Active.Count} Units and the active limit is "
                    + $"{_tuning.ActiveLimit}.");
            }

            if (army.Reserves.Count > _tuning.ReserveLimit)
            {
                throw new InvalidBattleInputException(
                    $"The {side} army holds {army.Reserves.Count} reserves and the reserve limit is "
                    + $"{_tuning.ReserveLimit}.");
            }

            if (army.Active.Count + army.Reserves.Count > _tuning.ArmyLimit)
            {
                throw new InvalidBattleInputException(
                    $"The {side} army brings {army.Active.Count + army.Reserves.Count} Units and the "
                    + $"army limit is {_tuning.ArmyLimit}.");
            }

            var taken = new HashSet<Hex>();

            foreach (var deployed in army.Active)
            {
                if (!_field.IsDeploymentHexFor(side, deployed.Hex))
                {
                    throw new InvalidBattleInputException(
                        $"{deployed.Hex} is not in the {side} deployment half.");
                }

                if (!taken.Add(deployed.Hex))
                {
                    throw new InvalidBattleInputException($"Two {side} Units are deployed on {deployed.Hex}.");
                }
            }

            foreach (var combatant in army.Active.Select(active => active.Combatant).Concat(army.Reserves))
            {
                if (combatant.Stats.Hp <= 0)
                {
                    throw new InvalidBattleInputException($"'{combatant.Name}' starts the battle with no HP.");
                }

                if (combatant.Stats.Range < 1)
                {
                    throw new InvalidBattleInputException($"'{combatant.Name}' has no attack range.");
                }

                if (combatant.Stats.AttackIntervalSeconds <= 0)
                {
                    throw new InvalidBattleInputException(
                        $"'{combatant.Name}' has an Attack Interval of zero, which is not a fast Unit "
                        + "but a broken clock.");
                }
            }
        }
    }
}
