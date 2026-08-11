/** Where a signed-in player lands. Task 3 replaces this seam with the real game shell. */
export const WORLD_PATH = "/world";

/** The query parameter that carries where the player was heading before being asked to sign in. */
export const RETURN_PARAM = "next";

/**
 * Accepts only a path inside this application.
 *
 * The value reaches us through a URL anyone can write, so anything that could resolve to
 * another origin — an absolute URL, a protocol-relative `//host`, or the `/\host` form some
 * browsers normalise the same way — is discarded rather than sanitised.
 */
export function safeRedirectTarget(raw: string | null | undefined): string | null {
  if (!raw || !raw.startsWith("/")) {
    return null;
  }

  if (raw.startsWith("//") || raw.startsWith("/\\")) {
    return null;
  }

  return raw;
}

export function loginPathFor(target: string): string {
  return `/login?${RETURN_PARAM}=${encodeURIComponent(target)}`;
}
