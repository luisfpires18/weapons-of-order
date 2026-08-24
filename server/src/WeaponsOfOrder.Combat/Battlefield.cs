namespace WeaponsOfOrder.Combat;

/// <summary>
/// One hex on the battlefield, in offset coordinates.
/// </summary>
/// <remarks>
/// The grid is 8 columns by 7 rows and the two armies face each other across the columns, so
/// the column is the axis that matters: a player's half is a range of columns, and closing the
/// distance means changing column. Rows run top to bottom within a column.
/// <para>
/// The layout is <em>odd-q</em>: flat-topped hexes arranged in columns, with odd-numbered
/// columns pushed half a hex down. Neighbour and distance arithmetic therefore goes through
/// cube coordinates rather than being written out per parity, because the parity forms are
/// where offset-grid bugs live.
/// </para>
/// </remarks>
public readonly record struct Hex(int Column, int Row)
{
    /// <summary>Whether this hex is on the battlefield described by <paramref name="field"/>.</summary>
    public bool IsOn(Battlefield field)
        => Column >= 0 && Column < field.Columns && Row >= 0 && Row < field.Rows;

    /// <summary>The cube coordinate of this hex, which is where the real arithmetic happens.</summary>
    public (int X, int Y, int Z) ToCube()
    {
        var x = Column;
        var z = Row - ((Column - (Column & 1)) / 2);

        return (x, -x - z, z);
    }

    public static Hex FromCube(int x, int z) => new(x, z + ((x - (x & 1)) / 2));

    /// <summary>Hex distance: the number of adjacent-hex steps on an empty board.</summary>
    /// <remarks>
    /// Canon measures range and target distance this way rather than by straight-line
    /// geometry, so this is the only distance the simulator uses.
    /// </remarks>
    public int DistanceTo(Hex other)
    {
        var (ax, ay, az) = ToCube();
        var (bx, by, bz) = other.ToCube();

        return (Math.Abs(ax - bx) + Math.Abs(ay - by) + Math.Abs(az - bz)) / 2;
    }

    /// <summary>
    /// The six hexes touching this one, in a fixed order, whether or not they are on the board.
    /// </summary>
    /// <remarks>
    /// The order is fixed and never sorted by anything about the battle. It is the tie-break of
    /// last resort inside the pathfinder, and canon is explicit that equally short routes carry
    /// no authored tactical preference — so it must be stable, and it must not become one.
    /// </remarks>
    public IEnumerable<Hex> Neighbours()
    {
        var (x, _, z) = ToCube();

        foreach (var (dx, dz) in CubeDirections)
        {
            yield return FromCube(x + dx, z + dz);
        }
    }

    private static readonly (int Dx, int Dz)[] CubeDirections =
    [
        (1, -1),
        (1, 0),
        (0, 1),
        (-1, 1),
        (-1, 0),
        (0, -1),
    ];

    public override string ToString() => $"({Column},{Row})";
}

/// <summary>Which army a combatant belongs to.</summary>
public enum BattleSide
{
    Player = 0,
    Opponent = 1,
}

/// <summary>
/// The shape of the battlefield: its size and the half each army deploys in.
/// </summary>
/// <remarks>
/// Canon fixes 8 columns, 7 rows, 56 hexes, and a 4 by 7 deployment half per side. Those are
/// the defaults; they are constructor parameters rather than constants so a test can build a
/// small board to prove a rule on, not because the real dimensions are open.
/// </remarks>
public sealed record Battlefield(int Columns = 8, int Rows = 7)
{
    /// <summary>The canonical battlefield.</summary>
    public static readonly Battlefield Canonical = new();

    public int HexCount => Columns * Rows;

    /// <summary>How many columns each army owns for deployment: exactly half the board.</summary>
    public int HalfColumns => Columns / 2;

    /// <summary>Every hex, in a fixed order, for enumeration that must not vary.</summary>
    public IEnumerable<Hex> Hexes()
    {
        for (var column = 0; column < Columns; column++)
        {
            for (var row = 0; row < Rows; row++)
            {
                yield return new Hex(column, row);
            }
        }
    }

    /// <summary>Whether <paramref name="hex"/> is inside <paramref name="side"/>'s deployment half.</summary>
    public bool IsDeploymentHexFor(BattleSide side, Hex hex)
        => hex.IsOn(this) && (side == BattleSide.Player
            ? hex.Column < HalfColumns
            : hex.Column >= HalfColumns);

    /// <summary>
    /// The column reserves enter through: the outermost column of that army's own half.
    /// </summary>
    /// <remarks>
    /// Canon puts reinforcement entry on the army's rear row and is explicit that a reserve
    /// never inherits the hex where a Unit fell. The rear of the player's half is column 0 and
    /// the rear of the opponent's is the last column, so a reinforcement always walks in from
    /// behind its own line.
    /// </remarks>
    public int RearColumnFor(BattleSide side)
        => side == BattleSide.Player ? 0 : Columns - 1;

    /// <summary>
    /// The entry hex assigned to the reserve at <paramref name="queuePosition"/>.
    /// </summary>
    /// <remarks>
    /// <strong>Prototype implementation detail, not canon.</strong> Canon says each reserve has
    /// a preferred rear-row entry hex and leaves how it is chosen unauthored. This walks the
    /// rear column from the top, wrapping if there are more reserves than rows, which is the
    /// smallest rule that is deterministic and gives every reserve a hex. When the creator
    /// authors a real assignment — most likely the player choosing one during deployment — it
    /// replaces this method and nothing else.
    /// </remarks>
    public Hex ReserveEntryHex(BattleSide side, int queuePosition)
        => new(RearColumnFor(side), queuePosition % Rows);
}
