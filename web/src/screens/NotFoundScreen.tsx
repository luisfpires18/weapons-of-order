import { Link } from "react-router";

/**
 * Terminal state for any path that is not the approved title screen.
 *
 * Task 1 removed the pre-Browser-V1 placeholder routes rather than keeping them alive
 * as de facto architecture, so every unimplemented destination lands here until the
 * task that owns it ships a real screen.
 */
export function NotFoundScreen() {
  return (
    <main className="flex h-dvh w-full flex-col items-center justify-center gap-8 px-6 text-center">
      <h1 className="font-display text-screen font-semibold tracking-[0.06em] text-bone">
        NOT FOUND
      </h1>
      <p className="max-w-[34rem] font-body text-body text-bone-dim">
        This destination does not exist yet.
      </p>
      <Link
        to="/"
        className="font-hud text-hud font-semibold uppercase tracking-[0.08em] text-bone transition-colors hover:text-selected focus:text-selected focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-selected"
      >
        Return to title
      </Link>
    </main>
  );
}
