import type { CSSProperties } from "react";
import { Link, Outlet, useLocation } from "react-router";
import { WORLD_PATH } from "@/auth/redirect";
import { AccountMenu } from "@/shell/AccountMenu";
import { destinationFor } from "@/shell/destinations";
import { PrimaryNav } from "@/shell/PrimaryNav";

/**
 * The lighting of the title screen without its photograph.
 *
 * Ember from the lower left where the forge opening sits, cool cave depth from the upper
 * right, both far enough down in opacity to be atmosphere rather than an image. Repeating
 * the cavern render behind every management screen would cost readability now and would
 * leave the Forge nothing to be, in Task 4, that the shell is not already doing louder.
 */
const ATMOSPHERE =
  "radial-gradient(60rem 42rem at 0% 100%," +
  " color-mix(in srgb, var(--color-ember-deep) 18%, transparent) 0%, transparent 68%)," +
  "radial-gradient(52rem 52rem at 100% 18%," +
  " color-mix(in srgb, var(--color-rune-deep) 15%, transparent) 0%, transparent 65%)";

/**
 * The frame every authenticated screen is drawn inside.
 *
 * Two measurements drive the layout, so they are declared once as custom properties: the
 * header's height, which the desktop navigation column sticks below, and the bottom bar's
 * height, which the content has to clear on a phone. Both fold in the device safe-area
 * insets, because the manifest asks for a translucent status bar and the bar itself sits on
 * the home-indicator edge.
 */
const SHELL_METRICS = {
  "--shell-header": "calc(3.5rem + env(safe-area-inset-top))",
  "--shell-nav": "calc(3.5rem + env(safe-area-inset-bottom))",
} as CSSProperties;

export function ShellLayout() {
  const location = useLocation();
  const context = destinationFor(location.pathname);

  return (
    <div className="relative min-h-dvh bg-void" style={SHELL_METRICS}>
      <div aria-hidden className="pointer-events-none fixed inset-0" style={{ background: ATMOSPHERE }} />

      <header className="sticky top-0 z-30 flex h-[var(--shell-header)] items-center justify-between gap-4 border-b-2 border-slate bg-ink/95 pt-[env(safe-area-inset-top)] pl-[max(1rem,env(safe-area-inset-left))] pr-[max(0.5rem,env(safe-area-inset-right))] backdrop-blur-sm lg:pl-6 lg:pr-4">
        <div className="flex min-w-0 items-baseline gap-3">
          <Link
            to={WORLD_PATH}
            className="shrink-0 font-display text-[0.75rem] font-semibold uppercase tracking-[0.32em] text-bone transition-colors hover:text-ember-bright focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-selected motion-reduce:transition-none sm:text-[0.8125rem] sm:tracking-[0.38em]"
          >
            Weapons of Order
          </Link>

          {/* The current screen is named once per viewport. On desktop the navigation column
              already marks it, so repeating it here would be a third label for one fact. */}
          {context ? (
            <p className="min-w-0 truncate font-hud text-hud font-semibold uppercase tracking-[0.18em] text-bone-dim lg:hidden">
              <span aria-hidden className="mr-2 text-ember">
                /
              </span>
              {context.label}
            </p>
          ) : null}
        </div>

        <AccountMenu />
      </header>

      <div className="relative lg:flex lg:items-start">
        <PrimaryNav />

        <main className="min-w-0 flex-1 pb-[var(--shell-nav)] lg:pb-0">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
