/**
 * The account rules the forms are allowed to state, mirroring the server's configuration in
 * `appsettings.json`. Client copy exists for speed and honesty, never for trust: the server
 * validates every one of these again, so a manipulated browser gains nothing by skipping them.
 *
 * They live together because the last time this copy was duplicated across two screens, one
 * of them kept advertising a policy that had changed.
 */

/** Length is the whole password rule. No character-class or diversity requirement. */
export const MINIMUM_PASSWORD_LENGTH = 6;

export const PASSWORD_HINT = `At least ${MINIMUM_PASSWORD_LENGTH} characters.`;

/**
 * The one structural restriction on a username, and the reason for it: sign-in takes a single
 * field, so anything carrying an `@` has to be an address.
 */
export const USERNAME_HINT = "Anything you like, as long as it has no @ in it.";
