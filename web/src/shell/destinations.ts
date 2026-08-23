import { WORLD_PATH } from "@/auth/redirect";

export const ACCOUNT_PATH = "/account";

export const FORGE_PATH = "/forge";

export const INVENTORY_PATH = "/inventory";

export const UNITS_PATH = "/units";

export type ShellDestination = {
  /** The route this destination owns. One destination, one path. */
  path: string;
  /** What the navigation calls it. UI copy, free to change; the path is the stable part. */
  label: string;
};

/**
 * The game destinations the primary navigation offers, in the order it offers them.
 *
 * Four, because four exist. Battle is Task 6 and is absent rather than present-and-disabled: a
 * control that cannot be used is worse than no control, and a menu padded out with future
 * systems misrepresents what the game can do.
 *
 * The order is the order the loop runs in — make something, see what you own, give it to
 * somebody — rather than alphabetical.
 *
 * Adding a destination later means adding an entry here and a route in `App.tsx`. Nothing in
 * the shell counts these or assumes any particular number, though the mobile bar is now at the
 * four-or-five destinations the layout document asks it to stay within; a fifth game
 * destination is where the rest start moving behind a More surface.
 */
export const GAME_DESTINATIONS: readonly ShellDestination[] = [
  { path: WORLD_PATH, label: "World" },
  { path: FORGE_PATH, label: "Forge" },
  { path: INVENTORY_PATH, label: "Inventory" },
  { path: UNITS_PATH, label: "Units" },
];

/**
 * Kept apart from the game destinations because it is not one, and because it stays put as
 * that list grows: the navigation pins it to the end on desktop.
 */
export const ACCOUNT_DESTINATION: ShellDestination = { path: ACCOUNT_PATH, label: "Account" };

export const SHELL_DESTINATIONS: readonly ShellDestination[] = [
  ...GAME_DESTINATIONS,
  ACCOUNT_DESTINATION,
];

/** The destination a path belongs to, for the shell's own context labelling. */
export function destinationFor(pathname: string): ShellDestination | undefined {
  return SHELL_DESTINATIONS.find((destination) => destination.path === pathname);
}
