import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router";
import { ApiProblem } from "@/api/problem";
import { RETURN_PARAM, safeRedirectTarget, WORLD_PATH } from "@/auth/redirect";
import { AUTH_URLS, postJson } from "@/auth/session";
import { useAntiforgeryTokens, useRefreshSession } from "@/auth/useSession";
import { AuthScreen } from "@/components/auth/AuthScreen";
import {
  Checkbox,
  FormNotice,
  INLINE_LINK_CLASSES,
  PrimaryAction,
  QUIET_ACTION_CLASSES,
  TextField,
} from "@/components/auth/FormControls";

export function LoginScreen() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const tokens = useAntiforgeryTokens();
  const refreshSession = useRefreshSession();

  const [identifier, setIdentifier] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(false);

  const destination = safeRedirectTarget(searchParams.get(RETURN_PARAM)) ?? WORLD_PATH;

  const signIn = useMutation({
    mutationFn: () => postJson(AUTH_URLS.login, { identifier, password, rememberMe }, tokens),
    onSuccess: async () => {
      // The session is re-read before navigating: it is what the route guard consults and
      // what carries an antiforgery token bound to the new identity.
      await refreshSession();
      void navigate(destination, { replace: true });
    },
  });

  const problem = signIn.error instanceof ApiProblem ? signIn.error : null;
  const unconfirmed = problem?.code === "email_not_confirmed";

  return (
    <AuthScreen
      title="Enter World"
      intro="Sign in to the account that holds your forge."
      footer={
        <>
          <p className="font-body text-body text-bone-dim">
            No account yet?{" "}
            <Link to="/register" className={INLINE_LINK_CLASSES}>
              Create one
            </Link>
          </p>
          <Link to="/forgot-password" className={`self-start ${QUIET_ACTION_CLASSES}`}>
            Forgot your password?
          </Link>
        </>
      }
    >
      <form
        noValidate
        className="flex flex-col gap-6"
        onSubmit={(event) => {
          event.preventDefault();
          signIn.mutate();
        }}
      >
        {problem ? (
          <FormNotice tone="error">
            {problem.message}
            {unconfirmed ? (
              <>
                {" "}
                <Link to="/confirm-email" className={INLINE_LINK_CLASSES}>
                  Resend the confirmation
                </Link>
              </>
            ) : null}
          </FormNotice>
        ) : null}

        {/* One field for both. The server reads anything containing an @ as an address and
            anything else as a username, which registration guarantees is unambiguous.
            `username` is the autocomplete token a password manager fills either form into. */}
        <TextField
          id="identifier"
          label="Username or email"
          type="text"
          value={identifier}
          onChange={setIdentifier}
          autoComplete="username"
        />

        <TextField
          id="password"
          label="Password"
          type="password"
          value={password}
          onChange={setPassword}
          autoComplete="current-password"
        />

        <Checkbox id="remember-me" label="Keep me signed in" checked={rememberMe} onChange={setRememberMe} />

        <PrimaryAction pending={signIn.isPending} pendingLabel="Signing in">
          Sign in
        </PrimaryAction>
      </form>
    </AuthScreen>
  );
}
