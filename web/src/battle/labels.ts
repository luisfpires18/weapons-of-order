/** Small display helpers the battle screens share. */

/**
 * A Unit's name, short enough to sit inside a hex on a phone.
 *
 * Three characters, because a hex at 320 pixels wide is about 46 across and the name has to read
 * at a glance rather than be reconstructed. The full name is beside it in every panel.
 */
export function abbreviate(name: string): string {
  const trimmed = name.trim();

  return trimmed.length <= 4 ? trimmed : trimmed.slice(0, 3);
}
