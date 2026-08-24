using Xunit;

namespace WeaponsOfOrder.Combat.Tests;

/// <summary>
/// The board itself: its size, its halves, and the adjacency and distance every other rule is
/// measured in.
/// </summary>
/// <remarks>
/// Offset-hex arithmetic is where grid bugs hide, and every one of them shows up later as a
/// targeting or pathing rule that seems to work until it does not. These assertions are on the
/// coordinates directly.
/// </remarks>
public class BattlefieldGeometryTests
{
    [Fact]
    public void The_canonical_battlefield_is_eight_columns_by_seven_rows()
    {
        var field = Battlefield.Canonical;

        Assert.Equal(8, field.Columns);
        Assert.Equal(7, field.Rows);
        Assert.Equal(56, field.HexCount);
        Assert.Equal(56, field.Hexes().Distinct().Count());
    }

    [Fact]
    public void Each_side_owns_a_four_by_seven_deployment_half()
    {
        var field = Battlefield.Canonical;

        var player = field.Hexes().Where(hex => field.IsDeploymentHexFor(BattleSide.Player, hex)).ToList();
        var opponent = field.Hexes().Where(hex => field.IsDeploymentHexFor(BattleSide.Opponent, hex)).ToList();

        Assert.Equal(28, player.Count);
        Assert.Equal(28, opponent.Count);
        Assert.Empty(player.Intersect(opponent));
        Assert.Equal(field.HexCount, player.Count + opponent.Count);

        Assert.All(player, hex => Assert.InRange(hex.Column, 0, 3));
        Assert.All(opponent, hex => Assert.InRange(hex.Column, 4, 7));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(8, 0)]
    [InlineData(0, 7)]
    public void A_hex_outside_the_grid_is_not_on_the_battlefield(int column, int row)
        => Assert.False(new Hex(column, row).IsOn(Battlefield.Canonical));

    [Fact]
    public void A_hex_is_no_distance_from_itself()
        => Assert.Equal(0, new Hex(3, 3).DistanceTo(new Hex(3, 3)));

    [Fact]
    public void Every_neighbour_is_exactly_one_hex_away()
    {
        var field = Battlefield.Canonical;

        foreach (var hex in field.Hexes())
        {
            var neighbours = hex.Neighbours().Where(candidate => candidate.IsOn(field)).ToList();

            Assert.All(neighbours, neighbour => Assert.Equal(1, hex.DistanceTo(neighbour)));

            // Six on an infinite grid; fewer at an edge, two in a corner, and never a repeat.
            Assert.Equal(neighbours.Count, neighbours.Distinct().Count());
            Assert.InRange(neighbours.Count, 2, 6);
        }
    }

    [Fact]
    public void Adjacency_is_mutual()
    {
        var field = Battlefield.Canonical;

        foreach (var hex in field.Hexes())
        {
            foreach (var neighbour in hex.Neighbours().Where(candidate => candidate.IsOn(field)))
            {
                Assert.Contains(hex, neighbour.Neighbours());
            }
        }
    }

    [Fact]
    public void Distance_is_symmetric_and_never_negative()
    {
        var field = Battlefield.Canonical;
        var hexes = field.Hexes().ToList();

        foreach (var one in hexes)
        {
            foreach (var other in hexes)
            {
                var distance = one.DistanceTo(other);

                Assert.Equal(distance, other.DistanceTo(one));
                Assert.True(distance >= 0);
            }
        }
    }

    /// <summary>
    /// Distance has to agree with the board a Unit actually walks on, not only with the cube
    /// arithmetic that computes it.
    /// </summary>
    /// <remarks>
    /// The check is a breadth-first search over the empty grid: if the shortest walk from A to B
    /// is not the same number as <see cref="Hex.DistanceTo"/>, then range, targeting and pursuit
    /// are all measuring something the Units cannot do.
    /// </remarks>
    [Fact]
    public void Hex_distance_equals_the_number_of_steps_across_an_empty_board()
    {
        var field = Battlefield.Canonical;

        foreach (var origin in field.Hexes())
        {
            var steps = new Dictionary<Hex, int> { [origin] = 0 };
            var queue = new Queue<Hex>([origin]);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var neighbour in current.Neighbours().Where(hex => hex.IsOn(field)))
                {
                    if (steps.TryAdd(neighbour, steps[current] + 1))
                    {
                        queue.Enqueue(neighbour);
                    }
                }
            }

            Assert.Equal(field.HexCount, steps.Count);
            Assert.All(steps, step => Assert.Equal(step.Value, origin.DistanceTo(step.Key)));
        }
    }

    [Fact]
    public void Offset_and_cube_coordinates_round_trip()
    {
        foreach (var hex in Battlefield.Canonical.Hexes())
        {
            var (x, _, z) = hex.ToCube();

            Assert.Equal(hex, Hex.FromCube(x, z));
        }
    }

    [Fact]
    public void Reserves_enter_through_the_rear_column_of_their_own_half()
    {
        var field = Battlefield.Canonical;

        Assert.Equal(0, field.RearColumnFor(BattleSide.Player));
        Assert.Equal(7, field.RearColumnFor(BattleSide.Opponent));

        foreach (var side in (BattleSide[])[BattleSide.Player, BattleSide.Opponent])
        {
            for (var queue = 0; queue < 8; queue++)
            {
                var entry = field.ReserveEntryHex(side, queue);

                Assert.Equal(field.RearColumnFor(side), entry.Column);
                Assert.True(field.IsDeploymentHexFor(side, entry));
            }

            // The first seven get a row each; the eighth wraps, which is what makes a queue longer
            // than the rear row possible at all.
            Assert.Equal(
                Enumerable.Range(0, field.Rows),
                Enumerable.Range(0, field.Rows).Select(queue => field.ReserveEntryHex(side, queue).Row));
            Assert.Equal(field.ReserveEntryHex(side, 0), field.ReserveEntryHex(side, field.Rows));
        }
    }
}
