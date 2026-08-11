import { useSession } from "@/auth/useSession";
import { Fact, FactList, ShellScreen } from "@/shell/ShellScreen";

/**
 * Where a signed-in player lands, and the first screen the shell was built to hold.
 *
 * It is short because the game is not written yet. The alternative — resource counters, an
 * army summary, an activity feed — would all be invented, and a screen that lies about what
 * exists is worse than a screen that says plainly what does. What it does prove is real: the
 * session reached the client, the shell is around it, and there is a coherent place for the
 * forge, the units and the battlefield to attach.
 */
export function WorldScreen() {
  const { data } = useSession();

  return (
    <ShellScreen title="World" lead="You are signed in. The game attaches here as each system is built.">
      <div className="flex max-w-[44rem] flex-col gap-10">
        <p className="font-body text-body leading-relaxed text-bone-dim">
          The forge, your units and the battlefield are not available yet. Each one appears in the
          navigation when it is ready to be used.
        </p>

        <FactList>
          <Fact label="Signed in as">{data?.account?.email}</Fact>
        </FactList>
      </div>
    </ShellScreen>
  );
}
