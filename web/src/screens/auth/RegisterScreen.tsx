import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { Link } from "react-router";
import { ApiProblem } from "@/api/problem";
import { AUTH_URLS, postJson } from "@/auth/session";
import { useAntiforgeryTokens } from "@/auth/useSession";
import { AuthScreen } from "@/components/auth/AuthScreen";
import { DevelopmentLinkHint } from "@/components/auth/DevelopmentLinkHint";
import {
  FormNotice,
  INLINE_LINK_CLASSES,
  PrimaryAction,
  QUIET_ACTION_CLASSES,
  TextField,
} from "@/components/auth/FormControls";

/** Mirrors the server policy in appsettings.json. Client validation is for speed, not trust. */
const MINIMUM_PASSWORD_LENGTH = 12;

export function RegisterScreen() {
  const tokens = useAntiforgeryTokens();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [mismatch, setMismatch] = useState(false);

  const register = useMutation({
    mutationFn: () => postJson(AUTH_URLS.register, { email, password }, tokens),
  });

  const problem = register.error instanceof ApiProblem ? register.error : null;

  if (register.isSuccess) {
    return (
      <AuthScreen
        title="Check your email"
        footer={
          <Link to="/login" className={`self-start ${QUIET_ACTION_CLASSES}`}>
            Back to sign in
          </Link>
        }
      >
        <div className="flex flex-col gap-6">
          {/* Deliberately says "if": the same answer is given when the address is already
              registered, so this screen cannot be used to test who has an account. */}
          <FormNotice tone="info">
            If that address can be registered, a confirmation link is on its way. Confirm it, then
            sign in.
          </FormNotice>
          {import.meta.env.DEV ? <DevelopmentLinkHint kind="EmailConfirmation" /> : null}
        </div>
      </AuthScreen>
    );
  }

  return (
    <AuthScreen
      title="New Account"
      intro="One account holds your forge, your units and your army."
      footer={
        <p className="font-body text-body text-bone-dim">
          Already have an account?{" "}
          <Link to="/login" className={INLINE_LINK_CLASSES}>
            Sign in
          </Link>
        </p>
      }
    >
      <form
        noValidate
        className="flex flex-col gap-6"
        onSubmit={(event) => {
          event.preventDefault();

          const passwordsMatch = password === confirmation;
          setMismatch(!passwordsMatch);

          if (passwordsMatch) {
            register.mutate();
          }
        }}
      >
        {problem && Object.keys(problem.fieldErrors).length === 0 ? (
          <FormNotice tone="error">{problem.message}</FormNotice>
        ) : null}

        <TextField
          id="email"
          label="Email"
          type="email"
          value={email}
          onChange={setEmail}
          autoComplete="email"
          error={problem?.fieldError("email")}
        />

        <TextField
          id="password"
          label="Password"
          type="password"
          value={password}
          onChange={setPassword}
          autoComplete="new-password"
          hint={`At least ${MINIMUM_PASSWORD_LENGTH} characters. Length beats symbols.`}
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

        <PrimaryAction pending={register.isPending} pendingLabel="Creating account">
          Create account
        </PrimaryAction>
      </form>
    </AuthScreen>
  );
}
