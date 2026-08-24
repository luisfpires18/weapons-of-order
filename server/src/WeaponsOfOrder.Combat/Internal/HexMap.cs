namespace WeaponsOfOrder.Combat.Internal;

/// <summary>
/// A value per hex, backed by a flat array.
/// </summary>
/// <remarks>
/// The battlefield is 56 hexes and the simulator walks it several times a tick, so a dictionary
/// keyed by a struct would be the wrong shape for something this small and this hot. Indexing
/// is <c>column * rows + row</c>, which is also a stable enumeration order.
/// </remarks>
internal sealed class HexMap<T>(Battlefield field)
    where T : class
{
    private readonly T?[] _values = new T?[field.Columns * field.Rows];

    public T? this[Hex hex]
    {
        get => _values[Index(hex)];
        set => _values[Index(hex)] = value;
    }

    private int Index(Hex hex) => (hex.Column * field.Rows) + hex.Row;
}

/// <summary>
/// Where one Unit can walk to, and how far each of those hexes is.
/// </summary>
/// <remarks>
/// A breadth-first search over free hexes, which is the whole of the pathfinding this game
/// needs: every step costs the same, occupied hexes are impassable, and canon is explicit that
/// equally short routes carry no authored tactical preference.
/// <para>
/// Determinism is earned in two places. Neighbours are always visited in
/// <see cref="Hex.Neighbours"/>'s fixed order and the first discovery of a hex keeps its parent,
/// so the same board always yields the same tree and therefore the same route; and
/// <see cref="Reachable"/> is materialised in the board's own hex order rather than in discovery
/// order, so a caller choosing between equally good destinations chooses predictably.
/// </para>
/// </remarks>
internal sealed class ReachMap
{
    private const int Unreachable = -1;

    private readonly Battlefield _field;
    private readonly int[] _steps;
    private readonly Hex?[] _cameFrom;
    private readonly List<(Hex Hex, int Steps)> _reachable = [];

    private ReachMap(Battlefield field)
    {
        _field = field;
        _steps = new int[field.Columns * field.Rows];
        _cameFrom = new Hex?[field.Columns * field.Rows];
    }

    /// <summary>Where <paramref name="origin"/> can walk to, treating occupied hexes as walls.</summary>
    /// <param name="blocked">Whether a hex is impassable. The origin's own hex is always included.</param>
    public static ReachMap From(Battlefield field, Hex origin, Func<Hex, bool> blocked)
    {
        var map = new ReachMap(field);
        Array.Fill(map._steps, Unreachable);
        map._steps[map.Index(origin)] = 0;

        var queue = new Queue<Hex>();
        queue.Enqueue(origin);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var next = map._steps[map.Index(current)] + 1;

            foreach (var neighbour in current.Neighbours())
            {
                if (!neighbour.IsOn(field) || blocked(neighbour) || map._steps[map.Index(neighbour)] != Unreachable)
                {
                    continue;
                }

                map._steps[map.Index(neighbour)] = next;
                map._cameFrom[map.Index(neighbour)] = current;
                queue.Enqueue(neighbour);
            }
        }

        foreach (var hex in field.Hexes())
        {
            var steps = map._steps[map.Index(hex)];

            if (steps != Unreachable)
            {
                map._reachable.Add((hex, steps));
            }
        }

        return map;
    }

    /// <summary>Every hex this Unit could stand on, including the one it is on, in board order.</summary>
    public IReadOnlyList<(Hex Hex, int Steps)> Reachable => _reachable;

    /// <summary>Steps to <paramref name="hex"/>, or null when nothing can get there.</summary>
    public int? StepsTo(Hex hex)
    {
        var steps = _steps[Index(hex)];

        return steps == Unreachable ? null : steps;
    }

    /// <summary>
    /// The single adjacent hex to move to in order to head for <paramref name="destination"/>,
    /// or null when the destination is unreachable or already underfoot.
    /// </summary>
    public Hex? FirstStepTowards(Hex destination)
    {
        if (StepsTo(destination) is null or 0)
        {
            return null;
        }

        var current = destination;

        while (_cameFrom[Index(current)] is { } previous)
        {
            if (_steps[Index(previous)] == 0)
            {
                return current;
            }

            current = previous;
        }

        return null;
    }

    private int Index(Hex hex) => (hex.Column * _field.Rows) + hex.Row;
}
