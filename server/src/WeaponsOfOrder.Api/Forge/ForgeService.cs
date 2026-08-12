using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WeaponsOfOrder.Infrastructure.Gameplay;
using WeaponsOfOrder.Infrastructure.Persistence;

namespace WeaponsOfOrder.Api.Forge;

/// <summary>
/// The ordinary forge: choose, pay, heat, strike, keep what you made.
/// </summary>
/// <remarks>
/// Every method takes the account id the endpoint read from the session cookie, and every
/// query is filtered by it. There is no path by which a caller names the account, the
/// workpiece, the temperature or the result; the only things the browser gets to say are
/// which recipe to start, whether it is holding the piece in the fire, and when to strike.
/// </remarks>
internal sealed class ForgeService(
    WeaponsOfOrderDbContext db,
    IOptions<ForgeOptions> options,
    TimeProvider clock)
{
    /// <summary>How much of a player's own work the forge screen shows back to them.</summary>
    private const int RecentItemLimit = 24;

    private ForgeOptions Options => options.Value;

    /// <summary>
    /// The whole forge screen in one read: stock, what can be made, and the workpiece.
    /// </summary>
    /// <remarks>
    /// Reports a burned-through workpiece as ruined without writing that down. The ruin is
    /// already implied by the stored anchor, so the projection is stable however many times
    /// it is read; the next thing the player actually does is what persists it.
    /// </remarks>
    public async Task<ForgeStatePayload> GetStateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var materials = await EnsureMaterialsAsync(userId, cancellationToken);
        var session = await CurrentSessionAsync(userId, cancellationToken);

        return await BuildStateAsync(materials, session, cancellationToken);
    }

    /// <summary>Pays for a recipe and puts a fresh workpiece on the anvil.</summary>
    public async Task<ForgeStatePayload> BeginAsync(
        Guid userId,
        string? recipeKey,
        CancellationToken cancellationToken)
    {
        var (key, recipe) = ResolveRecipe(recipeKey);
        var now = clock.GetUtcNow();
        var materials = await EnsureMaterialsAsync(userId, cancellationToken);
        var active = await ActiveSessionAsync(userId, cancellationToken);

        if (active is not null)
        {
            // A workpiece that burned through while the player was away is not a reason to
            // refuse them a new one. It is closed out here rather than left blocking the anvil.
            if (!Advance(active, now))
            {
                throw ForgeProblems.InProgress();
            }

            Finish(active, ForgeSessionStatus.Ruined, now);

            // Written before the new session is created rather than batched with it: the
            // unique index allows one active session, and the old row has to stop being one
            // before the new row starts, in that order.
            await SaveAsync(cancellationToken);
        }

        var cost = recipe.Cost;
        if (materials.Metal < cost.Metal || materials.Wood < cost.Wood || materials.Leather < cost.Leather)
        {
            throw ForgeProblems.InsufficientMaterials(recipe.DisplayName, ToPayload(cost));
        }

        materials.Metal -= cost.Metal;
        materials.Wood -= cost.Wood;
        materials.Leather -= cost.Leather;

        var session = new ForgeSession
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = userId,
            RecipeKey = key,
            Status = ForgeSessionStatus.Active,
            StartedAt = now,
            Temperature = 0,
            TemperatureAt = now,
            IsHeating = false,
            BurnSeconds = 0,
            StrikesTaken = 0,
        };

        db.ForgeSessions.Add(session);

        // One SaveChanges, so one transaction. If the unique index on the active session
        // rejects a duplicate begin, the deduction above rolls back with it and the
        // duplicate request is charged nothing.
        await SaveAsync(cancellationToken);

        return await BuildStateAsync(materials, session, cancellationToken);
    }

    /// <summary>Puts the workpiece into the fire, or takes it out.</summary>
    public async Task<ForgeStatePayload> SetHeatingAsync(
        Guid userId,
        bool heating,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var session = await RequireActiveSessionAsync(userId, cancellationToken);

        if (Advance(session, now))
        {
            return await RuinAsync(userId, session, now, cancellationToken);
        }

        session.IsHeating = heating;
        await SaveAsync(cancellationToken);

        return await BuildStateAsync(await EnsureMaterialsAsync(userId, cancellationToken), session, cancellationToken);
    }

    /// <summary>
    /// Strikes the workpiece, and completes the operation when the last blow lands.
    /// </summary>
    public async Task<ForgeStatePayload> StrikeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var session = await RequireActiveSessionAsync(userId, cancellationToken);

        if (Advance(session, now))
        {
            return await RuinAsync(userId, session, now, cancellationToken);
        }

        // Cheap rejection of a double-submitted press before it reaches the database. The
        // unique ordinal index is what actually guarantees one blow per press; this is what
        // keeps a leaned-on button from being a stream of conflicts.
        if (session.LastStrikeAt is { } last
            && (now - last).TotalSeconds < Options.Strike.CooldownSeconds)
        {
            throw ForgeProblems.StrikeCooldown();
        }

        var band = ForgeRules.BandFor(session.Temperature, Options.Heat);
        var ordinal = session.StrikesTaken + 1;

        var strike = new ForgeStrike
        {
            Id = Guid.CreateVersion7(),
            ForgeSessionId = session.Id,
            Ordinal = ordinal,
            Band = band,
            Temperature = session.Temperature,
            StruckAt = now,
        };

        // Added to the collection so the quality rule below sees the whole sequence, and to
        // the set explicitly so it is tracked as an insert. Reaching a tracked parent's
        // navigation with a key already assigned is not enough on its own: EF reads a set key
        // as "this row exists" and would issue an UPDATE against a row that does not.
        session.Strikes.Add(strike);
        db.ForgeStrikes.Add(strike);

        session.StrikesTaken = ordinal;
        session.LastStrikeAt = now;

        // The blow takes heat out of the piece, which is what makes the loop a rhythm rather
        // than three taps at one temperature.
        session.Temperature = Math.Max(0, session.Temperature - Options.Heat.LossPerStrike);

        if (ordinal >= Options.Strike.Required)
        {
            var craftsmanship = ForgeRules.CraftsmanshipFor(
                session.Strikes.OrderBy(blow => blow.Ordinal).Select(blow => blow.Band),
                Options.Craftsmanship);

            session.Craftsmanship = craftsmanship;
            Finish(session, ForgeSessionStatus.Completed, now);

            var (_, recipe) = ResolveRecipe(session.RecipeKey);

            db.ForgedItems.Add(new ForgedItem
            {
                Id = Guid.CreateVersion7(),
                OwnerUserId = userId,
                ForgeSessionId = session.Id,
                RecipeKey = session.RecipeKey,
                WeaponType = recipe.WeaponType,
                Craftsmanship = craftsmanship,
                Origin = ForgedItemOrigin.OrdinaryForge,
                ForgedAt = now,
            });
        }

        // Strike, completion and item are one write. A replayed request loses the unique
        // ordinal index and rolls the item back with it, so a session cannot mint twice.
        await SaveAsync(cancellationToken);

        return await BuildStateAsync(await EnsureMaterialsAsync(userId, cancellationToken), session, cancellationToken);
    }

    /// <summary>
    /// Sets the current workpiece aside. Its materials are spent and are not returned.
    /// </summary>
    public async Task<ForgeStatePayload> AbandonAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var session = await RequireActiveSessionAsync(userId, cancellationToken);

        Finish(session, Advance(session, now) ? ForgeSessionStatus.Ruined : ForgeSessionStatus.Abandoned, now);
        await SaveAsync(cancellationToken);

        return await BuildStateAsync(await EnsureMaterialsAsync(userId, cancellationToken), session, cancellationToken);
    }

    /// <summary>The caller's own forged items, newest first.</summary>
    public async Task<IReadOnlyList<ForgedItemPayload>> ListItemsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var items = await db.ForgedItems
            .AsNoTracking()
            .Where(item => item.OwnerUserId == userId)
            .OrderByDescending(item => item.ForgedAt)
            .ThenByDescending(item => item.Id)
            .Take(RecentItemLimit)
            .ToListAsync(cancellationToken);

        return [.. items.Select(item => new ForgedItemPayload(
            item.Id,
            item.RecipeKey,
            RecipeName(item.RecipeKey, item.WeaponType),
            item.WeaponType,
            Name(item.Craftsmanship),
            Name(item.Origin),
            item.ForgedAt))];
    }

    /// <summary>
    /// Grants the temporary opening stock the first time a player opens the forge.
    /// </summary>
    /// <remarks>
    /// Lazy, and deliberately not part of registration. There is no economy yet, so where
    /// materials come from is a placeholder; keeping it here means the account flows do not
    /// carry a gameplay concern, and replacing the grant with a real source later touches
    /// this method and nothing else.
    /// </remarks>
    private async Task<PlayerMaterials> EnsureMaterialsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await db.PlayerMaterials
            .FirstOrDefaultAsync(materials => materials.OwnerUserId == userId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var start = Options.StartingMaterials;
        var granted = new PlayerMaterials
        {
            OwnerUserId = userId,
            Metal = start.Metal,
            Wood = start.Wood,
            Leather = start.Leather,
            GrantedAt = clock.GetUtcNow(),
        };

        db.PlayerMaterials.Add(granted);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return granted;
        }
        catch (DbUpdateException)
        {
            // Two first loads at once. The primary key settled it; read back the winner
            // rather than reporting a conflict for something the player did not do.
            db.Entry(granted).State = EntityState.Detached;
            return await db.PlayerMaterials
                .FirstAsync(materials => materials.OwnerUserId == userId, cancellationToken);
        }
    }

    private Task<ForgeSession?> CurrentSessionAsync(Guid userId, CancellationToken cancellationToken)
        => db.ForgeSessions
            .Include(session => session.Strikes)
            .Where(session => session.OwnerUserId == userId)
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private Task<ForgeSession?> ActiveSessionAsync(Guid userId, CancellationToken cancellationToken)
        => db.ForgeSessions
            .Include(session => session.Strikes)
            .FirstOrDefaultAsync(
                session => session.OwnerUserId == userId && session.Status == ForgeSessionStatus.Active,
                cancellationToken);

    private async Task<ForgeSession> RequireActiveSessionAsync(Guid userId, CancellationToken cancellationToken)
        => await ActiveSessionAsync(userId, cancellationToken) ?? throw ForgeProblems.NoActiveForge();

    /// <summary>
    /// Moves a session's stored heat anchor forward to <paramref name="now"/>.
    /// </summary>
    /// <returns><see langword="true"/> when the workpiece has burned past the grace period.</returns>
    private bool Advance(ForgeSession session, DateTimeOffset now)
    {
        var projection = ForgeRules.Project(session, now, Options);

        session.Temperature = projection.Temperature;
        session.BurnSeconds = projection.BurnSeconds;
        session.TemperatureAt = now;

        return projection.Ruined;
    }

    private static void Finish(ForgeSession session, ForgeSessionStatus status, DateTimeOffset now)
    {
        session.Status = status;
        session.FinishedAt = now;
        session.IsHeating = false;
    }

    private async Task<ForgeStatePayload> RuinAsync(
        Guid userId,
        ForgeSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Finish(session, ForgeSessionStatus.Ruined, now);
        await SaveAsync(cancellationToken);

        return await BuildStateAsync(await EnsureMaterialsAsync(userId, cancellationToken), session, cancellationToken);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique indexes on the active session and on the strike ordinal, and the
            // non-negative material constraint, are the guarantees this API rests on. When
            // one of them fires it means another request got there first. A concurrency
            // exception lands here too: DbUpdateConcurrencyException derives from this, and
            // it means the same thing — the row this request planned against has moved.
            throw ForgeProblems.Conflict();
        }
    }

    private (string Key, ForgeRecipeSettings Recipe) ResolveRecipe(string? recipeKey)
    {
        var key = recipeKey?.Trim() ?? string.Empty;

        return Options.Recipes.TryGetValue(key, out var recipe)
            ? (key, recipe)
            : throw ForgeProblems.UnknownRecipe();
    }

    private string RecipeName(string recipeKey, string fallback)
        => Options.Recipes.TryGetValue(recipeKey, out var recipe) ? recipe.DisplayName : fallback;

    private async Task<ForgeStatePayload> BuildStateAsync(
        PlayerMaterials materials,
        ForgeSession? session,
        CancellationToken cancellationToken)
    {
        var heat = Options.Heat;

        var recipes = Options.Recipes
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new RecipePayload(
                entry.Key,
                entry.Value.DisplayName,
                entry.Value.WeaponType,
                ToPayload(entry.Value.Cost),
                materials.Metal >= entry.Value.Metal
                    && materials.Wood >= entry.Value.Wood
                    && materials.Leather >= entry.Value.Leather))
            .ToList();

        return new ForgeStatePayload(
            new MaterialsPayload(materials.Metal, materials.Wood, materials.Leather),
            recipes,
            session is null ? null : await ToPayloadAsync(session, cancellationToken),
            new ForgeTuningPayload(
                heat.MaxTemperature,
                heat.WorkableFrom,
                heat.IdealFrom,
                heat.BurningFrom,
                heat.HeatRatePerSecond,
                heat.CoolRatePerSecond,
                heat.BurnGraceSeconds,
                Options.Strike.CooldownSeconds));
    }

    private async Task<ForgeSessionPayload> ToPayloadAsync(
        ForgeSession session,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        // An active session is reported as it stands right now, including a ruin that has
        // not been written down yet. A finished one is reported as it was left.
        var live = session.Status == ForgeSessionStatus.Active;
        var projection = live
            ? ForgeRules.Project(session, now, Options)
            : new HeatProjection(
                session.Temperature,
                session.BurnSeconds,
                ForgeRules.BandFor(session.Temperature, Options.Heat),
                session.Status == ForgeSessionStatus.Ruined);

        var status = live && projection.Ruined
            ? ForgeSessionStatus.Ruined
            : session.Status;

        var itemId = session.Status == ForgeSessionStatus.Completed
            ? await db.ForgedItems
                .AsNoTracking()
                .Where(item => item.ForgeSessionId == session.Id)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        Options.Recipes.TryGetValue(session.RecipeKey, out var recipe);

        return new ForgeSessionPayload(
            session.Id,
            session.RecipeKey,
            recipe?.DisplayName ?? session.RecipeKey,
            recipe?.WeaponType ?? string.Empty,
            Name(status),
            Math.Round(projection.Temperature, 2),
            Name(projection.Band),
            live && session.IsHeating,
            live ? now : session.TemperatureAt,
            session.StrikesTaken,
            Options.Strike.Required,
            [.. session.Strikes
                .OrderBy(strike => strike.Ordinal)
                .Select(strike => new StrikePayload(strike.Ordinal, Name(strike.Band)))],
            Math.Round(projection.BurnSeconds, 2),
            session.Craftsmanship is { } craftsmanship ? Name(craftsmanship) : null,
            itemId);
    }

    private static MaterialsPayload ToPayload(MaterialAmounts amounts)
        => new(amounts.Metal, amounts.Wood, amounts.Leather);

    /// <summary>
    /// Enum names, lower-cased, so the wire contract is a stable string the client can switch
    /// on rather than an ordinal that moves when the enum is edited.
    /// </summary>
    private static string Name<T>(T value)
        where T : struct, Enum
        => value.ToString().ToLowerInvariant();
}
