import { useSyncExternalStore } from "react";

/**
 * Whether the viewport is wide enough for the desktop shell.
 *
 * Almost everything responsive in this application is CSS, and should stay that way. This exists
 * for the one thing CSS cannot do: change what is *in* the navigation rather than how it looks.
 * The phone's bottom bar carries four destinations and a More surface; the desktop column carries
 * all of them. Rendering both and hiding one would put every destination in the accessibility tree
 * twice, which is worse than a media query.
 *
 * Matches the `lg` breakpoint the shell's own classes use. Keep the two in step.
 */
const DESKTOP = "(min-width: 64rem)";

export function useWideViewport(): boolean {
  return useSyncExternalStore(subscribe, matches, () => true);
}

function subscribe(onChange: () => void): () => void {
  if (typeof window === "undefined" || typeof window.matchMedia !== "function") {
    return () => {};
  }

  const query = window.matchMedia(DESKTOP);
  query.addEventListener("change", onChange);

  return () => query.removeEventListener("change", onChange);
}

/**
 * Falls back to the wide shell where there is nothing to ask.
 *
 * Every browser has `matchMedia`, so this only decides what a headless DOM sees. The full column
 * is the right answer there: it is the shape that contains every destination, so a test that does
 * not care about the breakpoint still finds them all.
 */
function matches(): boolean {
  return typeof window === "undefined" || typeof window.matchMedia !== "function"
    ? true
    : window.matchMedia(DESKTOP).matches;
}
