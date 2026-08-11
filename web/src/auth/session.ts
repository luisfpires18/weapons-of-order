import { z } from "zod";
import { ApiProblem, readProblem } from "@/api/problem";

/**
 * Same-origin by design, like every other call: Vite proxies `/api` in development and the
 * ASP.NET Core host serves both the client and the API in deployment. That is what lets the
 * session ride on a cookie instead of a token the client has to store.
 */
export const SESSION_URL = "/api/auth/session";

export const AUTH_URLS = {
  register: "/api/auth/register",
  login: "/api/auth/login",
  logout: "/api/auth/logout",
  forgotPassword: "/api/auth/forgot-password",
  resetPassword: "/api/auth/reset-password",
  confirmEmail: "/api/auth/confirm-email",
  resendConfirmation: "/api/auth/resend-confirmation",
} as const;

/** Echoes the antiforgery request token the session response handed out. */
export const ANTIFORGERY_HEADER = "X-WoO-Antiforgery";

const sessionSchema = z.object({
  authenticated: z.boolean(),
  account: z
    .object({
      id: z.string(),
      email: z.string(),
      emailConfirmed: z.boolean(),
    })
    .nullable(),
  csrfToken: z.string(),
});

export type Session = z.infer<typeof sessionSchema>;
export type SessionAccount = NonNullable<Session["account"]>;

export async function fetchSession(signal?: AbortSignal): Promise<Session> {
  const response = await fetch(SESSION_URL, {
    headers: { Accept: "application/json" },
    signal,
  });

  if (!response.ok) {
    throw await readProblem(response);
  }

  return sessionSchema.parse(await response.json());
}

/**
 * Supplies antiforgery request tokens. `refresh` must go back to the server rather than
 * return a cached value: a token is bound to the identity it was minted under, so signing
 * in or out invalidates the one already held.
 */
export type AntiforgeryTokens = {
  current: () => Promise<string>;
  refresh: () => Promise<string>;
};

/**
 * Sends a mutation with its antiforgery token, retrying once against a fresh token.
 *
 * The retry exists because the token the client holds goes stale the moment the session's
 * identity changes. Refreshing and replaying once is the correct response to that, and it
 * is bounded: a second rejection is reported rather than looped on.
 */
export async function postJson<T>(
  path: string,
  body: unknown,
  tokens: AntiforgeryTokens,
  signal?: AbortSignal,
): Promise<T | null> {
  const send = (token: string) =>
    fetch(path, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Accept: "application/json",
        [ANTIFORGERY_HEADER]: token,
      },
      body: JSON.stringify(body),
      signal,
    });

  let response = await send(await tokens.current());

  if (response.status === 400) {
    const problem = await readProblem(response);
    if (problem.code !== "antiforgery") {
      throw problem;
    }
    response = await send(await tokens.refresh());
  }

  if (!response.ok) {
    throw await readProblem(response);
  }

  if (response.status === 204) {
    return null;
  }

  return (await response.json()) as T;
}

export function isProblemCode(error: unknown, code: string): boolean {
  return error instanceof ApiProblem && error.code === code;
}
