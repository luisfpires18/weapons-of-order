import { Link } from "react-router";
import { ApiProblem } from "@/api/problem";
import { useSession } from "@/auth/useSession";
import { FormNotice, INLINE_LINK_CLASSES, PrimaryAction } from "@/components/auth/FormControls";
import { Fact, FactList, ShellScreen } from "@/shell/ShellScreen";
import { useSignOut } from "@/shell/useSignOut";

/**
 * Account and settings.
 *
 * Everything on it comes from the session the server already publishes: the username the
 * player is known by, the address the account is recovered through, and whether that address
 * is confirmed. Nothing here is editable — there is no avatar, no display name separate from
 * the username and no preferences, because none of those exist in the account schema, and a
 * settings screen offering controls which change nothing is a worse answer than a short one
 * that says so.
 *
 * It is also the phone's route to signing out. The bottom bar reaches it in one tap and the
 * action here is a full-width control, which is a better target than a popover hung off a
 * compact header.
 */
export function AccountScreen() {
  const { data } = useSession();
  const signOut = useSignOut();

  const account = data?.account;
  const problem = signOut.error instanceof ApiProblem ? signOut.error : null;

  return (
    <ShellScreen title="Account" lead="The account that holds your forge.">
      <div className="flex max-w-[36rem] flex-col gap-10">
        <FactList>
          <Fact label="Username">{account?.username}</Fact>
          <Fact label="Email">{account?.email}</Fact>
          <Fact label="Email confirmation">
            {account?.emailConfirmed ? "Confirmed" : "Not confirmed"}
          </Fact>
        </FactList>

        {account && !account.emailConfirmed ? (
          <FormNotice tone="info">
            Confirm your email address to keep access to this account.{" "}
            <Link to="/confirm-email" className={INLINE_LINK_CLASSES}>
              Send the confirmation again
            </Link>
          </FormNotice>
        ) : null}

        <p className="font-body text-body leading-relaxed text-bone-dim">
          There is nothing else to configure yet. Settings arrive with the systems they control.
        </p>

        {problem ? <FormNotice tone="error">{problem.message}</FormNotice> : null}

        <form
          className="flex max-w-[22rem] flex-col"
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
    </ShellScreen>
  );
}
