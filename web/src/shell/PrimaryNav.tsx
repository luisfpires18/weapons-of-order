import { useEffect, useId, useState } from "react";
import { NavLink, useLocation } from "react-router";
import type { ShellDestination } from "@/shell/destinations";
import {
  ACCOUNT_DESTINATION,
  BAR_DESTINATIONS,
  GAME_DESTINATIONS,
  MORE_DESTINATIONS,
} from "@/shell/destinations";
import { useWideViewport } from "@/shell/useViewport";

/**
 * One navigation, two shapes.
 *
 * Below `lg` it is a bottom bar, because a thumb reaches the bottom of a phone and not the side of
 * it. From `lg` it is a left column, because the width is there and a persistent column costs
 * nothing that the content misses. It is the same element either way — not a desktop sidebar
 * squeezed into a phone, and not a second navigation tree to keep in step with the first.
 *
 * The lists do differ now, and deliberately. Battle is the sixth destination, which is where the
 * layout document says the bar should stop growing and the rest should move behind More: six
 * labels sharing 320 pixels is a row of truncations, not a navigation. Desktop keeps all of them.
 *
 * Which list is rendered is the one responsive decision here made in JavaScript rather than CSS,
 * because it is about what the navigation contains rather than how it looks. Rendering both and
 * hiding one would announce every destination twice.
 *
 * The boundary between navigation and content is a hairline of slate, and the current destination
 * lights its own length of it in ember. That is the shell's one ornament: the separator carries the
 * state instead of a pill, a badge or an icon set that does not exist yet.
 */
export function PrimaryNav() {
  const wide = useWideViewport();

  return (
    <nav
      aria-label="Primary"
      className="fixed inset-x-0 bottom-0 z-30 h-[var(--shell-nav)] border-t-2 border-slate bg-ink/95 pb-[env(safe-area-inset-bottom)] backdrop-blur-sm lg:sticky lg:inset-x-auto lg:bottom-auto lg:top-[var(--shell-header)] lg:h-[calc(100dvh-var(--shell-header))] lg:w-[13.5rem] lg:shrink-0 lg:border-r-2 lg:border-t-0 lg:pb-6"
    >
      {wide ? (
        // Everything, with Account pinned to the far end of the column.
        <ul className="flex h-full flex-col gap-1 pt-4">
          {GAME_DESTINATIONS.map((destination) => (
            <NavItem key={destination.path} destination={destination} />
          ))}
          <NavItem destination={ACCOUNT_DESTINATION} pinned />
        </ul>
      ) : (
        // The phone's bar: four destinations and a way to the rest.
        <ul className="flex h-full">
          {BAR_DESTINATIONS.map((destination) => (
            <NavItem key={destination.path} destination={destination} />
          ))}
          <MoreItem />
        </ul>
      )}
    </nav>
  );
}

/**
 * `pinned` is what keeps Account at the far end of the desktop column as game destinations are
 * added above it.
 */
function NavItem({ destination, pinned }: { destination: ShellDestination; pinned?: boolean }) {
  return (
    <li
      className={`min-w-0 flex-1 lg:flex-none ${pinned ? "lg:mt-auto lg:border-t-2 lg:border-slate/70 lg:pt-4" : ""}`}
    >
      <NavLink to={destination.path} className={itemClasses}>
        {({ isActive }) => (
          <>
            <Rail lit={isActive} />
            <span className="truncate">{destination.label}</span>
          </>
        )}
      </NavLink>
    </li>
  );
}

/**
 * More, and what is behind it.
 *
 * A real disclosure rather than a link to a menu page: the destinations inside are one tap away
 * and the player never loses the screen they were on to reach them. It lights like a destination
 * when the current one is inside it, so More is never the only thing on the bar that is not
 * telling the player where they are.
 */
function MoreItem() {
  const location = useLocation();
  const panelId = useId();

  // What is remembered is the screen the menu was opened on, not a boolean. Navigating therefore
  // closes it by itself, however the navigation happened — no effect watching the location, and no
  // render where the menu is still open over a screen it does not belong to.
  const [openedOn, setOpenedOn] = useState<string | null>(null);
  const open = openedOn === location.pathname;

  const holdsCurrent = MORE_DESTINATIONS.some(
    (destination) => destination.path === location.pathname,
  );

  const setOpen = (showing: boolean) => setOpenedOn(showing ? location.pathname : null);

  useEffect(() => {
    if (!open) {
      return;
    }

    const close = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpenedOn(null);
      }
    };

    document.addEventListener("keydown", close);

    return () => document.removeEventListener("keydown", close);
  }, [open]);

  return (
    <li className="min-w-0 flex-1">
      {open ? (
        <>
          {/* Named rather than silent: a full-screen invisible control that dismisses the menu is
              something a screen reader should be able to find and use, not a trap. */}
          <button
            type="button"
            aria-label="Close menu"
            onClick={() => setOpen(false)}
            className="fixed inset-0 z-40 cursor-default bg-void/70 backdrop-blur-sm"
          />

          <div
            id={panelId}
            className="fixed inset-x-0 bottom-[var(--shell-nav)] z-50 border-t-2 border-slate bg-ink pb-2"
          >
            <ul className="flex flex-col">
              {MORE_DESTINATIONS.map((destination) => (
                <li key={destination.path}>
                  <NavLink
                    to={destination.path}
                    className={({ isActive }) =>
                      [
                        "flex min-h-14 items-center px-[max(1.25rem,env(safe-area-inset-left))]",
                        "font-hud text-[0.875rem] font-semibold uppercase tracking-[0.16em]",
                        "border-b border-slate/60 transition-colors motion-reduce:transition-none",
                        "focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-selected",
                        isActive ? "bg-ink-raised/70 text-bone" : "text-bone-dim hover:text-bone",
                      ].join(" ")
                    }
                  >
                    {destination.label}
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        </>
      ) : null}

      <button
        type="button"
        aria-expanded={open}
        aria-controls={open ? panelId : undefined}
        aria-current={holdsCurrent ? "page" : undefined}
        onClick={() => setOpen(!open)}
        className={[
          "group relative flex h-full min-h-14 w-full cursor-pointer items-center justify-center",
          "gap-3 px-1 font-hud text-[0.6875rem] font-semibold uppercase tracking-[0.08em]",
          "transition-colors motion-reduce:transition-none",
          "focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-selected",
          holdsCurrent || open ? "text-bone" : "text-bone-dim hover:text-bone",
        ].join(" ")}
      >
        <Rail lit={holdsCurrent} />
        <span className="truncate">More</span>
      </button>
    </li>
  );
}

function itemClasses({ isActive }: { isActive: boolean }): string {
  return [
    // Condensed and tighter on the bar than in the column: the destinations have to share the
    // width of a phone, and a label that wraps or overflows is worse than one set a size down in
    // the font the HUD already uses.
    "group relative flex h-full min-h-14 items-center justify-center gap-3 px-1 font-hud",
    "text-[0.6875rem] tracking-[0.08em] lg:px-6 lg:text-[0.875rem] lg:tracking-[0.16em]",
    "font-semibold uppercase transition-colors motion-reduce:transition-none",
    "focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-selected",
    "lg:min-h-12 lg:justify-start",
    isActive ? "text-bone lg:bg-ink-raised/70" : "text-bone-dim hover:text-bone lg:hover:bg-ink-raised/40",
  ].join(" ");
}

/**
 * The lit length of the boundary. Sits two pixels outside the link so it covers the navigation's
 * own border rather than sitting beside it.
 */
function Rail({ lit }: { lit: boolean }) {
  return (
    <span
      aria-hidden
      className="absolute -top-[2px] left-0 h-[2px] w-full transition-colors motion-reduce:transition-none lg:-right-[2px] lg:left-auto lg:top-0 lg:h-full lg:w-[2px]"
      style={
        lit
          ? {
              backgroundColor: "var(--color-ember)",
              boxShadow: "0 0 10px color-mix(in srgb, var(--color-ember) 55%, transparent)",
            }
          : undefined
      }
    />
  );
}
