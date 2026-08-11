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
  const { data, isPending } = useSession();
  const location = useLocation();

  if (isPending) {
    return <SessionPending />;
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
