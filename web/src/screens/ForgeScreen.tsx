import { useCallback } from "react";
import { Link } from "react-router";
import { ApiProblem } from "@/api/problem";
import { FormNotice, INLINE_LINK_CLASSES } from "@/components/auth/FormControls";
import type { ForgeRecipe, ForgeSession } from "@/forge/api";
import { Anvil, BlowRow } from "@/forge/Anvil";
import { CRAFTSMANSHIP_LABELS, CRAFTSMANSHIP_TEXT } from "@/forge/craftsmanship";
import { AnvilAction, HoldToHeat, QuietAction, Strike } from "@/forge/ForgeActions";
import { MaterialStock, RecentWork, RecipeCost } from "@/forge/ForgeRail";
import { STRIKE_COOLDOWN_CODE, useForgeActions, useForgeState } from "@/forge/useForge";
import { INVENTORY_PATH, UNITS_PATH } from "@/shell/destinations";
import { ShellScreen } from "@/shell/ShellScreen";

/**
 * The forge.
 *
 * The signature system of Weapons of Order, and the first screen in the application that is
 * a game rather than an account. The layout gives the anvil the room and keeps everything
 * else to a narrow rail beside it on desktop, or below it on a phone — where the gauge stays
 * in the upper half of the screen and the two controls sit under it, so a thumb can work
 * without covering what it needs to read.
 *
 * Nothing on this screen decides anything. Heat, band, quality, ruin and ownership are all
 * the server's, and every control here is a statement of intent that the server answers with
 * the new state of the forge.
 */
export function ForgeScreen() {
  const { data, isPending, isError, error, refetch } = useForgeState();

  const session = data?.session ?? null;
  const active = session?.status === "active";

  // One set of actions, sharing one queue, so they reach the server in the order they were
  // taken. See `useForgeActions`.
  const { begin, strike, abandon, heat } = useForgeActions(active);
  const { setHeating } = heat;

  // The burn rule runs on stored state, so the server has no reason to write a ruin down
  // until it is asked. Letting go of the fire is the smallest true thing to say at the
  // moment the iron burns through, and the answer to it carries the ruin.
  const reportBurnedThrough = useCallback(() => setHeating(false), [setHeating]);

  if (isPending) {
    return (
      <ShellScreen title="Forge">
        <p role="status" className="font-body text-body text-bone-dim">
          Lighting the forge
        </p>
      </ShellScreen>
    );
  }

  if (isError) {
    return (
      <ShellScreen title="Forge">
        <div className="flex max-w-[36rem] flex-col gap-6">
          <FormNotice tone="error">
            {error instanceof ApiProblem ? error.message : "The forge could not be read."}
          </FormNotice>
          <QuietAction onClick={() => void refetch()}>Try again</QuietAction>
        </div>
      </ShellScreen>
    );
  }

  // One recipe, because Task 4 is one vertical slice. The catalogue is a list on the wire,
  // so a second ordinary weapon becomes a chooser here rather than a rewrite.
  const recipe: ForgeRecipe | undefined = data.recipes[0];
  const problem = actionProblem(begin.error, strike.error, abandon.error, heat.error);

  return (
    <ShellScreen
      title="Forge"
      lead="Heat the iron, strike it while the colour is right, and keep what you make."
    >
      <div className="grid gap-12 lg:grid-cols-[minmax(0,1fr)_19rem] lg:gap-16">
        <section className="flex min-w-0 flex-col gap-8">
          {session?.status === "completed" ? (
            <Finished session={session} />
          ) : session?.status === "ruined" ? (
            <Ruined />
          ) : (
            <>
              <Anvil session={active ? session : null} tuning={data.tuning} onBurnedThrough={reportBurnedThrough} />
              {active ? <BlowRow strikes={session.strikes} required={session.strikesRequired} /> : null}
            </>
          )}

          {problem ? <FormNotice tone="error">{problem}</FormNotice> : null}

          {active ? (
            <div className="flex flex-col gap-6">
              <div className="flex gap-4">
                <HoldToHeat heating={session.heating} disabled={false} onChange={setHeating} />
                <Strike disabled={strike.isPending} onStrike={() => strike.run()} />
              </div>
              <QuietAction onClick={() => abandon.run()}>Set the workpiece aside</QuietAction>
            </div>
          ) : recipe ? (
            <div className="flex flex-col gap-4">
              <AnvilAction
                pending={begin.isPending}
                pendingLabel="Starting"
                disabled={!recipe.affordable}
                onClick={() => begin.run({ recipeKey: recipe.key })}
              >
                {session?.status === "completed" || session?.status === "ruined"
                  ? `Start another ${recipe.name.toLowerCase()}`
                  : `Start a ${recipe.name.toLowerCase()}`}
              </AnvilAction>

              <p className="font-body text-body text-bone-dim">
                {recipe.affordable
                  ? `Costs ${describeCost(recipe)}.`
                  : `You need ${describeCost(recipe)} and do not have it. There is no way to gather materials yet.`}
              </p>
            </div>
          ) : null}
        </section>

        <aside className="flex min-w-0 flex-col gap-10 lg:border-l-2 lg:border-slate/60 lg:pl-8">
          <MaterialStock materials={data.materials} />
          {recipe ? <RecipeCost recipe={recipe} /> : null}
          <RecentWork />
        </aside>
      </div>
    </ShellScreen>
  );
}

/**
 * What came off the anvil. The craftsmanship is the headline because it is the only thing
 * about this sword the player changed.
 */
function Finished({ session }: { session: ForgeSession }) {
  const craftsmanship = session.craftsmanship ?? "common";

  return (
    <div className="flex flex-col gap-4">
      <p className="font-hud text-hud font-semibold uppercase tracking-[0.16em] text-bone-dim">Finished</p>
      <p
        className={`font-display text-[2.5rem] font-semibold uppercase leading-none tracking-[0.1em] lg:text-[3.5rem] ${CRAFTSMANSHIP_TEXT[craftsmanship]}`}
      >
        {CRAFTSMANSHIP_LABELS[craftsmanship]}
      </p>
      <p className="font-display text-[1.25rem] uppercase tracking-[0.2em] text-bone">{session.name}</p>
      <p className="max-w-[32rem] font-body text-body leading-relaxed text-bone-dim">
        It is yours and it is kept. Find it in your{" "}
        <Link to={INVENTORY_PATH} className={INLINE_LINK_CLASSES}>
          inventory
        </Link>
        , or give it to a{" "}
        <Link to={UNITS_PATH} className={INLINE_LINK_CLASSES}>
          unit
        </Link>{" "}
        to carry.
      </p>
    </div>
  );
}

function Ruined() {
  return (
    <div className="flex flex-col gap-4">
      <p className="font-hud text-hud font-semibold uppercase tracking-[0.16em] text-bone-dim">Ruined</p>
      <p className="font-display text-[2.5rem] font-semibold uppercase leading-none tracking-[0.1em] text-ember-deep lg:text-[3.5rem]">
        Burned through
      </p>
      <p className="max-w-[32rem] font-body text-body leading-relaxed text-bone-dim">
        The iron sat in the fire too long and there is nothing left to work. Its materials are
        spent. Take the next piece out before it turns white.
      </p>
    </div>
  );
}

function describeCost(recipe: ForgeRecipe): string {
  const parts: string[] = [];

  if (recipe.cost.metal > 0) parts.push(`${recipe.cost.metal} Metal`);
  if (recipe.cost.wood > 0) parts.push(`${recipe.cost.wood} Wood`);
  if (recipe.cost.leather > 0) parts.push(`${recipe.cost.leather} Leather`);

  return parts.length > 0 ? parts.join(" and ") : "nothing";
}

/**
 * The first failure worth showing.
 *
 * A blow that arrived inside the cooldown is left out: the player pressed twice quickly, the
 * forge did the right thing, and an error about it would be the interface complaining about
 * something nobody did wrong.
 */
function actionProblem(...errors: (Error | null)[]): string | null {
  for (const error of errors) {
    if (error instanceof ApiProblem && error.code !== STRIKE_COOLDOWN_CODE) {
      return error.message;
    }
  }

  return null;
}
