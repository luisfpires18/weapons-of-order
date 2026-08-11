import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router";
import { ApiProblem } from "@/api/problem";
import { AUTH_URLS, postJson } from "@/auth/session";
import { clearPrivateState, useAntiforgeryTokens, useSession } from "@/auth/useSession";
import { AuthScreen } from "@/components/auth/AuthScreen";
import { FormNotice, PrimaryAction } from "@/components/auth/FormControls";

/**
 * Where ENTER WORLD leads once there is a session.
 *
 * Task 2 owns accounts, not the game. This is the smallest honest destination that proves
 * the session reached the client: it names the account and offers the way out. Task 3
 * replaces it with the real authenticated shell — deliberately no navigation, no counters
 * and no placeholder panels, because inventing them would mean inventing gameplay.
 */
export function WorldEntryScreen() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const tokens = useAntiforgeryTokens();
  const { data } = useSession();

  const signOut = useMutation({
    mutationFn: () => postJson(AUTH_URLS.logout, {}, tokens),
    onSuccess: () => {
      // Everything cached was fetched for this account. Dropping all of it is what stops
      // the next screen from briefly rendering the last session's data.
      clearPrivateState(queryClient);
      void navigate("/", { replace: true });
    },
  });

  const problem = signOut.error instanceof ApiProblem ? signOut.error : null;

  return (
    <AuthScreen title="World Entered" intro="Your account is live and the session is held by the server.">
      <div className="flex flex-col gap-8">
        <dl className="flex flex-col gap-2">
          <dt className="font-hud text-hud font-semibold uppercase tracking-[0.16em] text-bone-dim">
            Signed in as
          </dt>
          <dd className="font-body text-[1rem] break-all text-bone">{data?.account?.email}</dd>
        </dl>

        <p className="font-body text-body leading-relaxed text-bone-dim">
          The forge, your units and the battlefield are built on top of this. They arrive with the
          authenticated game shell.
        </p>

        {problem ? <FormNotice tone="error">{problem.message}</FormNotice> : null}

        <form
          className="flex flex-col"
          onSubmit={(event) => {
            event.preventDefault();
            signOut.mutate();
          }}
        >
          <PrimaryAction pending={signOut.isPending} pendingLabel="Signing out">
            Sign out
          </PrimaryAction>
        </form>
      </div>
    </AuthScreen>
  );
}
