import type { ReactNode } from "react";
import { Link } from "react-router";
import forgeBackground from "@art/backgrounds/forge-16x9.png";
import { EmberRule } from "@/components/EmberRule";

/**
 * One step further into the same cavern as the title screen: the forge art stays, pushed
 * down far enough that form text reads cleanly over it.
 */
const SCRIM =
  "linear-gradient(to bottom," +
  " color-mix(in srgb, var(--color-void) 90%, transparent) 0%," +
  " color-mix(in srgb, var(--color-void) 78%, transparent) 40%," +
  " color-mix(in srgb, var(--color-void) 95%, transparent) 100%)";

/**
 * The shared frame for every unauthenticated account screen.
 *
 * There is no card. The title screen sets the precedent that a surface does not need a box
 * around it, so the form is a column of type and rules floating on the environment.
 */
export function AuthScreen({
  title,
  intro,
  children,
  footer,
}: {
  title: string;
  intro?: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
}) {
  return (
    <main className="relative min-h-dvh w-full overflow-x-hidden">
      <img
        src={forgeBackground}
        alt=""
        aria-hidden
        className="fixed inset-0 h-full w-full object-cover"
      />
      <div aria-hidden className="fixed inset-0" style={{ background: SCRIM }} />

      <div className="relative z-10 flex min-h-dvh flex-col items-center gap-10 px-[max(1.5rem,env(safe-area-inset-left))] pt-[max(2.5rem,env(safe-area-inset-top))] pb-[max(2.5rem,env(safe-area-inset-bottom))] sm:justify-center">
        <Link
          to="/"
          className="font-display text-[0.8125rem] font-semibold uppercase tracking-[0.42em] text-bone-dim transition-colors hover:text-bone focus-visible:text-bone focus-visible:outline-2 focus-visible:outline-offset-6 focus-visible:outline-selected"
        >
          Weapons of Order
        </Link>

        <section className="flex w-full max-w-[26rem] flex-col gap-8">
          <header className="flex flex-col gap-4">
            <h1 className="font-display text-[1.75rem] font-semibold uppercase leading-tight tracking-[0.12em] text-bone sm:text-[2rem]">
              {title}
            </h1>
            <EmberRule />
            {intro ? <p className="font-body text-body leading-relaxed text-bone-dim">{intro}</p> : null}
          </header>

          {children}
        </section>

        {footer ? <div className="flex w-full max-w-[26rem] flex-col gap-3">{footer}</div> : null}
      </div>
    </main>
  );
}
