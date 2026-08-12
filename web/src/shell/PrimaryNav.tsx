import { NavLink } from "react-router";
import type { ShellDestination } from "@/shell/destinations";
import { ACCOUNT_DESTINATION, GAME_DESTINATIONS } from "@/shell/destinations";

/**
 * One navigation, two shapes.
 *
 * Below `lg` it is a bottom bar, because a thumb reaches the bottom of a phone and not the
 * side of it. From `lg` it is a left column, because the width is there and a persistent
 * column costs nothing that the content misses. It is the same element and the same list
 * either way — not a desktop sidebar squeezed into a phone, and not a second navigation tree
 * to keep in step with the first.
 *
 * The boundary between navigation and content is a hairline of slate, and the current
 * destination lights its own length of it in ember. That is the shell's one ornament: the
 * separator carries the state instead of a pill, a badge or an icon set that does not exist
 * yet. It runs vertically down the column and horizontally across the bar, so the same rule
 * reads at both sizes.
 */
export function PrimaryNav() {
  return (
    <nav
      aria-label="Primary"
      className="fixed inset-x-0 bottom-0 z-30 h-[var(--shell-nav)] border-t-2 border-slate bg-ink/95 pb-[env(safe-area-inset-bottom)] backdrop-blur-sm lg:sticky lg:inset-x-auto lg:bottom-auto lg:top-[var(--shell-header)] lg:h-[calc(100dvh-var(--shell-header))] lg:w-[13.5rem] lg:shrink-0 lg:border-r-2 lg:border-t-0 lg:pb-6"
    >
      <ul className="flex h-full lg:flex-col lg:gap-1 lg:pt-4">
        {GAME_DESTINATIONS.map((destination) => (
          <NavItem key={destination.path} destination={destination} />
        ))}
        <NavItem destination={ACCOUNT_DESTINATION} pinned />
      </ul>
    </nav>
  );
}

/**
 * `pinned` is what keeps Account at the far end of the desktop column as game destinations
 * are added above it. On the bottom bar it means nothing: everything there is a peer.
 */
function NavItem({ destination, pinned }: { destination: ShellDestination; pinned?: boolean }) {
  return (
    <li
      className={`min-w-0 flex-1 lg:flex-none ${pinned ? "lg:mt-auto lg:border-t-2 lg:border-slate/70 lg:pt-4" : ""}`}
    >
      <NavLink
        to={destination.path}
        className={({ isActive }) =>
          [
            // Condensed and tighter on the bar than in the column: five destinations have to
            // share the width of a phone, and a label that wraps or overflows is worse than
            // one set a size down in the font the HUD already uses.
            "group relative flex h-full min-h-14 items-center justify-center gap-3 px-1 font-hud",
            "text-[0.6875rem] tracking-[0.08em] lg:px-6 lg:text-[0.875rem] lg:tracking-[0.16em]",
            "font-semibold uppercase transition-colors motion-reduce:transition-none",
            "focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-selected",
            "lg:min-h-12 lg:justify-start",
            isActive
              ? "text-bone lg:bg-ink-raised/70"
              : "text-bone-dim hover:text-bone lg:hover:bg-ink-raised/40",
          ].join(" ")
        }
      >
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
 * The lit length of the boundary. Sits two pixels outside the link so it covers the
 * navigation's own border rather than sitting beside it.
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
