import { Link } from "react-router";
import { useSession } from "@/auth/useSession";
import { INLINE_LINK_CLASSES } from "@/components/auth/FormControls";
import { FORGE_PATH } from "@/shell/destinations";
import { Fact, FactList, ShellScreen } from "@/shell/ShellScreen";

/**
 * Where a signed-in player lands.
 *
 * Still short. The forge is real now and is named as such; units and the battlefield are
 * not, and are described as absent rather than dressed up with resource counters, an army
 * summary or an activity feed. A screen that lies about what exists is worse than a screen
 * that says plainly what does.
 */
export function WorldScreen() {
  const { data } = useSession();

  return (
    <ShellScreen title="World" lead="You are signed in. The game attaches here as each system is built.">
      <div className="flex max-w-[44rem] flex-col gap-10">
        <p className="font-body text-body leading-relaxed text-bone-dim">
          The{" "}
          <Link to={FORGE_PATH} className={INLINE_LINK_CLASSES}>
            forge
          </Link>{" "}
          is open, and what you make there is kept. Your units and the battlefield are not built
          yet; each one appears in the navigation when it is ready to be used.
        </p>

        <FactList>
          <Fact label="Signed in as">{data?.account?.email}</Fact>
        </FactList>
      </div>
    </ShellScreen>
  );
}
