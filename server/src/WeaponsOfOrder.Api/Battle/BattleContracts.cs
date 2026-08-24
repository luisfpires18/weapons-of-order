using System.Text.Json.Serialization;

namespace WeaponsOfOrder.Api.Battle;

/// <summary>One hex, in the offset coordinates the battlefield is addressed by.</summary>
internal sealed record HexPayload(int Column, int Row);

/// <summary>The board's shape, so the client draws the battlefield the server is simulating.</summary>
/// <remarks>
/// Published rather than hardcoded in the browser. The client has to agree with the server about
/// which hexes exist and which half is the player's, and there is only one authority for that.
/// </remarks>
internal sealed record BattlefieldPayload(int Columns, int Rows, int DeploymentColumns);

/// <summary>How many Units an army may field, hold back, and bring in total.</summary>
internal sealed record ArmyLimitsPayload(int Active, int Reserve, int Army);

/// <summary>The six universal combat stats plus Range, as the server totalled them.</summary>
/// <remarks>
/// Read-only, and read-only in both directions: the client displays these and never sends them. A
/// browser that could name its own Power would be the browser deciding the battle.
/// <para>
/// <paramref name="MovementSpeed"/> is the canonical stat rather than the Mounted state that
/// currently produces it: a multiple of standard movement, where higher is faster. Whether a Unit
/// is Mounted is identity and is published beside its name on
/// <see cref="ArmyUnitPayload.Mounted"/>.
/// </para>
/// </remarks>
internal sealed record CombatStatsPayload(
    int Hp,
    int Power,
    int Defense,
    double AttackIntervalSeconds,
    double CriticalChance,
    int Range,
    double MovementSpeed);

/// <summary>A weapon in a Unit's hands, named well enough for the deployment screen.</summary>
internal sealed record ArmyWeaponPayload(Guid ItemId, string Name, string Craftsmanship);

/// <summary>
/// One of the player's Units and where it stands in the army.
/// </summary>
/// <param name="Role">
/// <c>active</c>, <c>reserve</c>, or <c>unplaced</c> for a Unit the player owns but has not put
/// anywhere. Every owned Unit is listed, because the deployment screen is where they are placed.
/// </param>
internal sealed record ArmyUnitPayload(
    Guid UnitId,
    string DefinitionKey,
    string Name,
    string Kingdom,
    int Tier,
    bool Mounted,
    IReadOnlyList<ArmyWeaponPayload> Weapons,
    CombatStatsPayload Stats,
    string Role,
    HexPayload? Hex,
    int? ReserveOrder,
    HexPayload? ReserveEntryHex);

/// <summary>The player's army as it currently stands.</summary>
/// <param name="Ready">
/// Whether a battle could be fought with it. An army needs at least one Unit on the battlefield;
/// reserves alone would be an army that never turns up.
/// </param>
internal sealed record ArmyPayload(
    BattlefieldPayload Battlefield,
    ArmyLimitsPayload Limits,
    IReadOnlyList<ArmyUnitPayload> Units,
    bool Ready);

/// <summary>Where the player wants one Unit to stand.</summary>
internal sealed record ActivePlacementRequest(Guid UnitId, int Column, int Row);

/// <summary>
/// The whole army the player wants, replacing whatever was saved.
/// </summary>
/// <remarks>
/// A replacement rather than a list of edits. Placing, moving, removing and reordering are all
/// one shape, and there is no sequence of partial updates that can leave a deployment half-moved
/// — the server writes the army it validated or none of it.
/// <para>
/// Nothing here is authoritative except intent. The Units are checked against the caller's own
/// roster, and every stat is resolved server-side.
/// </para>
/// </remarks>
internal sealed record SaveArmyRequest(
    IReadOnlyList<ActivePlacementRequest>? Active,
    IReadOnlyList<Guid>? Reserves);

/// <summary>One combatant in a resolved battle.</summary>
/// <param name="Id">The battle's own identifier for it, which every event refers to.</param>
/// <param name="UnitId">
/// The player Unit it was, or null for the training opposition, which is not made of Units.
/// </param>
internal sealed record BattleCombatantPayload(
    string Id,
    string Side,
    Guid? UnitId,
    string Name,
    CombatStatsPayload Stats,
    int? ReserveOrder,
    HexPayload? ReserveEntryHex,
    string EndState,
    int FinalHp,
    int FinalEnergy,
    HexPayload? FinalHex);

/// <summary>
/// One thing that happened at one simulated moment.
/// </summary>
/// <remarks>
/// A discriminated union on <c>kind</c>, so each event carries only the fields it has and the
/// client can switch on it exhaustively. The alternative — one flat shape with every field
/// nullable — would make a renderer read like a series of guesses.
/// <para>
/// Two events with the same <see cref="Time"/> happened at the same simulation moment and must be
/// presented as one. That is not a rendering nicety: it is the difference between a mutual last
/// kill reading as a Draw and reading as somebody winning by a frame.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(DeployedEventPayload), "deployed")]
[JsonDerivedType(typeof(ReserveEnteredEventPayload), "reserve")]
[JsonDerivedType(typeof(MovedEventPayload), "moved")]
[JsonDerivedType(typeof(AttackEventPayload), "attack")]
[JsonDerivedType(typeof(DiedEventPayload), "died")]
[JsonDerivedType(typeof(EndedEventPayload), "ended")]
internal abstract record BattleEventPayload(int Time);

internal sealed record DeployedEventPayload(int Time, string Id, HexPayload Hex) : BattleEventPayload(Time);

internal sealed record ReserveEnteredEventPayload(int Time, string Id, HexPayload Hex) : BattleEventPayload(Time);

internal sealed record MovedEventPayload(int Time, string Id, HexPayload From, HexPayload To)
    : BattleEventPayload(Time);

/// <summary>
/// An attack and everything it did.
/// </summary>
/// <remarks>
/// The result, not the ingredients. The client is told the damage, the target's remaining HP and
/// the attacker's remaining Energy, so it never replays the damage pipeline to find out what
/// happened — which is the whole point of the server being authoritative.
/// </remarks>
internal sealed record AttackEventPayload(
    int Time,
    string AttackerId,
    string TargetId,
    string Attack,
    bool Critical,
    int Damage,
    int TargetHp,
    int AttackerEnergy)
    : BattleEventPayload(Time);

internal sealed record DiedEventPayload(int Time, string Id, HexPayload Hex) : BattleEventPayload(Time);

internal sealed record EndedEventPayload(int Time, string Outcome, string Reason) : BattleEventPayload(Time);

/// <summary>
/// The authoritative result of one battle, complete enough to draw from.
/// </summary>
/// <param name="Seed">
/// The seed the server used, as text because it does not fit a JavaScript number. Published so a
/// battle is identifiable and reproducible, never accepted back.
/// </param>
internal sealed record BattleResultPayload(
    string Outcome,
    string Reason,
    int DurationMilliseconds,
    string Seed,
    BattlefieldPayload Battlefield,
    IReadOnlyList<BattleCombatantPayload> Combatants,
    IReadOnlyList<BattleEventPayload> Events);
