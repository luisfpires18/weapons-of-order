/** Where a signed-in player lands. Task 3 replaces this seam with the real game shell. */
export const WORLD_PATH = "/world";

/** The query parameter that carries where the player was heading before being asked to sign in. */
export const RETURN_PARAM = "next";

/**
 * A second separator straight after the leading one, in any spelling that resolves to an
 * authority: `//host`, `/\host`, and their percent-encoded forms. The router, the history
 * API and the browser's own URL parser do not agree on which of these are separators, so
 * none of them are accepted.
 */
const LEADING_AUTHORITY = /^\/(?:\/|\\|%2f|%5c)/i;

/**
 * Characters browsers strip out of a URL before resolving it. Left in, `/\t/host` arrives
 * at the network stack as `//host`.
 */
function hasStrippableCharacter(value: string): boolean {
  for (const character of value) {
    const code = character.charCodeAt(0);
    if (code <= 0x20 || code === 0x7f) {
      return true;
    }
  }

  return false;
}

/**
 * Accepts only a path inside this application.
 *
 * The value reaches us through a URL anyone can write, so this is an allow-list: it must be
 * a single leading slash followed by something that cannot be read as a host. Everything
 * else — an absolute URL, a `javascript:` or `data:` URL, a bare host, a protocol-relative
 * form, or any encoding of one — is discarded rather than repaired, because a value that
 * needs repairing is not a value this application wrote.
 */
export function safeRedirectTarget(raw: string | null | undefined): string | null {
  if (!raw || !raw.startsWith("/")) {
    return null;
  }

  if (hasStrippableCharacter(raw) || LEADING_AUTHORITY.test(raw)) {
    return null;
  }

  return raw;
}

export function loginPathFor(target: string): string {
  return `/login?${RETURN_PARAM}=${encodeURIComponent(target)}`;
}
