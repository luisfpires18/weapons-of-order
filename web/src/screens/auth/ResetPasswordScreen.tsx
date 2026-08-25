import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { Link, useSearchParams } from "react-router";
import { ApiProblem } from "@/api/problem";
import { PASSWORD_HINT } from "@/auth/policy";
import { AUTH_URLS, postJson } from "@/auth/session";
import { useAntiforgeryTokens } from "@/auth/useSession";
import { AuthScreen } from "@/components/auth/AuthScreen";
import {
  FormNotice,
  INLINE_LINK_CLASSES,
  PrimaryAction,
  QUIET_ACTION_CLASSES,
  TextField,
} from "@/components/auth/FormControls";

export function ResetPasswordScreen() {
  const [searchParams] = useSearchParams();
  const tokens = useAntiforgeryTokens();

  const userId = searchParams.get("userId");
  const token = searchParams.get("token");

  const [password, setPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [mismatch, setMismatch] = useState(false);

  const reset = useMutation({
    mutationFn: () => postJson(AUTH_URLS.resetPassword, { userId, token, password }, tokens),
  });

  const problem = reset.error instanceof ApiProblem ? reset.error : null;

  const signInLink = (
    <Link to="/login" className={`self-start ${QUIET_ACTION_CLASSES}`}>
      Back to sign in
    </Link>
  );

  if (!userId || !token) {
    return (
      <AuthScreen title="Link not usable" footer={signInLink}>
        <FormNotice tone="error">
          This link is missing the details needed to reset a password.{" "}
          <Link to="/forgot-password" className={INLINE_LINK_CLASSES}>
            Request a new one
          </Link>
        </FormNotice>
      </AuthScreen>
    );
  }

  if (reset.isSuccess) {
    return (
      <AuthScreen title="Password updated" footer={signInLink}>
        <FormNotice tone="info">
          Your password is set. Any session opened with the old one has been dropped.
        </FormNotice>
      </AuthScreen>
    );
  }

  return (
    <AuthScreen title="Set New Password" intro="Choose the password for this account." footer={signInLink}>
      <form
        noValidate
        className="flex flex-col gap-6"
        onSubmit={(event) => {
          event.preventDefault();

          const passwordsMatch = password === confirmation;
          setMismatch(!passwordsMatch);

          if (passwordsMatch) {
            reset.mutate();
          }
        }}
      >
        {problem && Object.keys(problem.fieldErrors).length === 0 ? (
          <FormNotice tone="error">
            {problem.message}
            {problem.code === "invalid_token" ? (
              <>
                {" "}
                <Link to="/forgot-password" className={INLINE_LINK_CLASSES}>
                  Request a new link
                </Link>
              </>
            ) : null}
          </FormNotice>
        ) : null}

        <TextField
          id="password"
          label="New password"
          type="password"
          value={password}
          onChange={setPassword}
          autoComplete="new-password"
          hint={PASSWORD_HINT}
          error={problem?.fieldError("password")}
        />

        <TextField
          id="password-confirmation"
          label="Confirm password"
          type="password"
          value={confirmation}
          onChange={(value) => {
            setConfirmation(value);
            setMismatch(false);
          }}
          autoComplete="new-password"
          error={mismatch ? "Both passwords must match." : undefined}
        />

        <PrimaryAction pending={reset.isPending} pendingLabel="Setting password">
          Set password
        </PrimaryAction>
      </form>
    </AuthScreen>
  );
}
