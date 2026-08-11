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
  PrimaryAction,
  QUIET_ACTION_CLASSES,
  TextField,
} from "@/components/auth/FormControls";

export function ForgotPasswordScreen() {
  const tokens = useAntiforgeryTokens();
  const [email, setEmail] = useState("");

  const request = useMutation({
    mutationFn: () => postJson(AUTH_URLS.forgotPassword, { email }, tokens),
  });

  const problem = request.error instanceof ApiProblem ? request.error : null;

  const backToSignIn = (
    <Link to="/login" className={`self-start ${QUIET_ACTION_CLASSES}`}>
      Back to sign in
    </Link>
  );

  if (request.isSuccess) {
    return (
      <AuthScreen title="Check your email" footer={backToSignIn}>
        <div className="flex flex-col gap-6">
          {/* Never confirms whether the address is registered. */}
          <FormNotice tone="info">
            If that address belongs to an account, a reset link is on its way. The link works once.
          </FormNotice>
          {import.meta.env.DEV ? <DevelopmentLinkHint kind="PasswordReset" /> : null}
        </div>
      </AuthScreen>
    );
  }

  return (
    <AuthScreen
      title="Reset Password"
      intro="Give us the address on the account and we will send a link to set a new password."
      footer={backToSignIn}
    >
      <form
        noValidate
        className="flex flex-col gap-6"
        onSubmit={(event) => {
          event.preventDefault();
          request.mutate();
        }}
      >
        {problem ? <FormNotice tone="error">{problem.message}</FormNotice> : null}

        <TextField
          id="email"
          label="Email"
          type="email"
          value={email}
          onChange={setEmail}
          autoComplete="email"
        />

        <PrimaryAction pending={request.isPending} pendingLabel="Sending">
          Send reset link
        </PrimaryAction>
      </form>
    </AuthScreen>
  );
}
