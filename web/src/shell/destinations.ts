import { WORLD_PATH } from "@/auth/redirect";

export const ACCOUNT_PATH = "/account";

export const FORGE_PATH = "/forge";

export const INVENTORY_PATH = "/inventory";

export const UNITS_PATH = "/units";

export const BATTLE_PATH = "/battle";

export type ShellDestination = {
  /** The route this destination owns. One destination, one path. */
  path: string;
  /** What the navigation calls it. UI copy, free to change; the path is the stable part. */
  label: string;
};

/**
 * The game destinations the primary navigation offers, in the order it offers them.
 *
 * Five, because five exist. The order is the order the loop runs in — make something, see what you
 * own, give it to somebody, send them out — rather than alphabetical.
 *
 * Adding a destination later means adding an entry here and a route in `App.tsx`, plus deciding
 * whether it belongs on the phone's bottom bar or behind More.
 */
export const GAME_DESTINATIONS: readonly ShellDestination[] = [
  { path: WORLD_PATH, label: "World" },
  { path: FORGE_PATH, label: "Forge" },
  { path: INVENTORY_PATH, label: "Inventory" },
  { path: UNITS_PATH, label: "Units" },
  { path: BATTLE_PATH, label: "Battle" },
];

/**
 * Kept apart from the game destinations because it is not one, and because it stays put as that
 * list grows: the navigation pins it to the end on desktop.
 */
export const ACCOUNT_DESTINATION: ShellDestination = { path: ACCOUNT_PATH, label: "Account" };

export const SHELL_DESTINATIONS: readonly ShellDestination[] = [
  ...GAME_DESTINATIONS,
  ACCOUNT_DESTINATION,
];

/**
 * What the phone's bottom bar carries.
 *
 * Four, plus More. The layout document asks the bar to stay within four or five destinations and
 * says that beyond that the rest should move behind a More surface — Battle is the destination
 * that reached it, so this is where the split happens. Desktop has the width for all of them and
 * keeps the full column.
 *
 * The four here are the ones a player returns to: the world, the forge, the units they equip, and
 * the battle they send them into.
 */
export const BAR_DESTINATIONS: readonly ShellDestination[] = [
  { path: WORLD_PATH, label: "World" },
  { path: FORGE_PATH, label: "Forge" },
  { path: UNITS_PATH, label: "Units" },
  { path: BATTLE_PATH, label: "Battle" },
];

/** What More holds on a phone. Reachable in one more tap, and never a dead end. */
export const MORE_DESTINATIONS: readonly ShellDestination[] = [
  { path: INVENTORY_PATH, label: "Inventory" },
  ACCOUNT_DESTINATION,
];

/** The destination a path belongs to, for the shell's own context labelling. */
export function destinationFor(pathname: string): ShellDestination | undefined {
  return SHELL_DESTINATIONS.find((destination) => destination.path === pathname);
}
