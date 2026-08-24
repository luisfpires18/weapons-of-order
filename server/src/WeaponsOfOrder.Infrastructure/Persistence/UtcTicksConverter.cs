using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace WeaponsOfOrder.Infrastructure.Persistence;

/// <summary>
/// Stores a <see cref="DateTimeOffset"/> as the number of ticks since the Unix-era epoch in
/// UTC.
/// </summary>
/// <remarks>
/// <para>
/// SQLite has no date type. Left alone, EF Core writes a <c>DateTimeOffset</c> as text and
/// then refuses to order by it: <c>SQLite does not support expressions of type
/// 'DateTimeOffset' in ORDER BY clauses</c>. That is not an edge case here — the forge asks
/// for a player's most recent session on every load, and the inventory asks for their most
/// recently forged items.
/// </para>
/// <para>
/// An integer orders natively and correctly, and round-trips exactly. The alternative, a
/// fixed-width ISO string, reads better in a table viewer but makes ordering depend on the
/// format never varying by a character.
/// </para>
/// <para>
/// The offset is not stored, because there is nothing to store: every value written by this
/// application comes from <c>DateTimeOffset.UtcNow</c>. A value with a real offset would come
/// back as the same instant expressed in UTC.
/// </para>
/// <para>
/// This is a SQLite accommodation and belongs to the prototype. A PostgreSQL production
/// database has a native type with an offset and should drop it.
/// </para>
/// </remarks>
internal sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
    value => value.UtcTicks,
    ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
