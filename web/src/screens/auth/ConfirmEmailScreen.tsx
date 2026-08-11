import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { Link, useSearchParams } from "react-router";
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

/**
 * Confirmation is a button press, not something that happens on page load.
 *
 * Mail scanners and link previewers fetch URLs before anybody reads them; a token spent by
 * a scanner leaves the player with a link that no longer works. Requiring the press also
 * keeps this a real form submission rather than a POST fired by navigation.
 */
export function ConfirmEmailScreen() {
  const [searchParams] = useSearchParams();
  const tokens = useAntiforgeryTokens();

  const userId = searchParams.get("userId");
  const token = searchParams.get("token");

  const confirm = useMutation({
    mutationFn: () => postJson(AUTH_URLS.confirmEmail, { userId, token }, tokens),
  });

  const problem = confirm.error instanceof ApiProblem ? confirm.error : null;

  const signInLink = (
    <Link to="/login" className={`self-start ${QUIET_ACTION_CLASSES}`}>
      Back to sign in
    </Link>
  );

  if (confirm.isSuccess) {
    return (
      <AuthScreen title="Email confirmed" footer={signInLink}>
        <FormNotice tone="info">This address is confirmed. You can sign in now.</FormNotice>
      </AuthScreen>
    );
  }

  if (!userId || !token) {
    return <ResendConfirmation />;
  }

  return (
    <AuthScreen
      title="Confirm Email"
      intro="Confirm this address to finish setting up the account."
      footer={signInLink}
    >
      <form
        className="flex flex-col gap-6"
        onSubmit={(event) => {
          event.preventDefault();
          confirm.mutate();
        }}
      >
        {problem ? (
          <FormNotice tone="error">
            {problem.message}{" "}
            <Link to="/confirm-email" className={INLINE_LINK_CLASSES}>
              Send a new link
            </Link>
          </FormNotice>
        ) : null}

        <PrimaryAction pending={confirm.isPending} pendingLabel="Confirming">
          Confirm this address
        </PrimaryAction>
      </form>
    </AuthScreen>
  );
}

/** Reached without a link, or after one expired. */
function ResendConfirmation() {
  const tokens = useAntiforgeryTokens();
  const [email, setEmail] = useState("");

  const resend = useMutation({
    mutationFn: () => postJson(AUTH_URLS.resendConfirmation, { email }, tokens),
  });

  const problem = resend.error instanceof ApiProblem ? resend.error : null;

  const signInLink = (
    <Link to="/login" className={`self-start ${QUIET_ACTION_CLASSES}`}>
      Back to sign in
    </Link>
  );

  if (resend.isSuccess) {
    return (
      <AuthScreen title="Check your email" footer={signInLink}>
        <div className="flex flex-col gap-6">
          <FormNotice tone="info">
            If that address needs confirming, a new link is on its way.
          </FormNotice>
          {import.meta.env.DEV ? <DevelopmentLinkHint kind="EmailConfirmation" /> : null}
        </div>
      </AuthScreen>
    );
  }

  return (
    <AuthScreen
      title="Confirm Email"
      intro="Enter the address on the account and we will send another confirmation link."
      footer={signInLink}
    >
      <form
        noValidate
        className="flex flex-col gap-6"
        onSubmit={(event) => {
          event.preventDefault();
          resend.mutate();
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

        <PrimaryAction pending={resend.isPending} pendingLabel="Sending">
          Send confirmation link
        </PrimaryAction>
      </form>
    </AuthScreen>
  );
}
