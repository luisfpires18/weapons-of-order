import { Navigate, Outlet, useLocation } from "react-router";
import { loginPathFor, WORLD_PATH } from "@/auth/redirect";
import { useSession } from "@/auth/useSession";

/**
 * Gates a route on the server's answer, not on the client's opinion.
 *
 * Hiding a link is not access control, and neither is this: the API rejects an
 * unauthenticated caller on its own. This only decides which screen to render, so a player
 * typing a protected URL gets the login screen instead of an empty page full of errors.
 */
export function RequireAuth() {
  const { data, isPending, isError, refetch } = useSession();
  const location = useLocation();

  if (isPending) {
    return <SessionPending />;
  }

  // A session that could not be read is not a session that says no. Sending the player to
  // the login screen would tell them their sign-in lapsed when the truth is that the server
  // is unreachable, and the login form they arrive at would fail for the same reason.
  if (isError) {
    return <SessionUnavailable onRetry={() => void refetch()} />;
  }

  if (!data?.authenticated) {
    return <Navigate to={loginPathFor(`${location.pathname}${location.search}`)} replace />;
  }

  return <Outlet />;
}

/** Keeps a signed-in player out of the sign-in screens. */
export function RequireAnonymous() {
  const { data, isPending } = useSession();

  if (isPending) {
    return <SessionPending />;
  }

  if (data?.authenticated) {
    return <Navigate to={WORLD_PATH} replace />;
  }

  return <Outlet />;
}

/**
 * Deliberately quiet. This is visible for one request on a cold load, and a spinner that
 * appears for 80ms reads as a flicker rather than as feedback.
 *
 * It is also why nothing of the shell is drawn until the answer arrives: a top bar and a
 * navigation column rendered ahead of the session would have to be taken away again from
 * whoever turns out not to be signed in.
 */
function SessionPending() {
  return (
    <main className="flex h-dvh w-full items-center justify-center bg-void">
      <p role="status" className="font-hud text-hud font-semibold uppercase tracking-[0.24em] text-bone-dim">
        Checking session
      </p>
    </main>
  );
}

/** The shell's error state: what went wrong, and the one action that can fix it. */
function SessionUnavailable({ onRetry }: { onRetry: () => void }) {
  return (
    <main className="flex h-dvh w-full flex-col items-center justify-center gap-6 bg-void px-6 text-center">
      <h1 className="font-display text-[1.5rem] font-semibold uppercase tracking-[0.12em] text-bone">
        No answer from the server
      </h1>
      <p className="max-w-[30rem] font-body text-body leading-relaxed text-bone-dim">
        Your session could not be confirmed. Check your connection and try again.
      </p>
      <button
        type="button"
        onClick={onRetry}
        className="min-h-12 cursor-pointer rounded-panel border-[length:var(--border-panel)] border-ember bg-transparent px-8 font-hud text-[0.9375rem] font-semibold uppercase tracking-[0.18em] text-ember-bright transition-colors hover:bg-ember hover:text-void focus-visible:bg-ember focus-visible:text-void focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ember-bright motion-reduce:transition-none"
      >
        Try again
      </button>
    </main>
  );
}
