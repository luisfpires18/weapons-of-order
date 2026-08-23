import { Link } from "react-router";
import { useSession } from "@/auth/useSession";
import { INLINE_LINK_CLASSES } from "@/components/auth/FormControls";
import { FORGE_PATH, INVENTORY_PATH, UNITS_PATH } from "@/shell/destinations";
import { Fact, FactList, ShellScreen } from "@/shell/ShellScreen";

/**
 * Where a signed-in player lands.
 *
 * Still short, and still only about what is actually there. Forging, an inventory and a roster
 * are real and are named; the battlefield is not, and is described as absent rather than
 * dressed up with resource counters, an army summary or an activity feed. A screen that lies
 * about what exists is worse than a screen that says plainly what does.
 */
export function WorldScreen() {
  const { data } = useSession();

  return (
    <ShellScreen title="World" lead="You are signed in. The game attaches here as each system is built.">
      <div className="flex max-w-[44rem] flex-col gap-10">
        <p className="font-body text-body leading-relaxed text-bone-dim">
          Work at the{" "}
          <Link to={FORGE_PATH} className={INLINE_LINK_CLASSES}>
            forge
          </Link>{" "}
          and what you make is kept in your{" "}
          <Link to={INVENTORY_PATH} className={INLINE_LINK_CLASSES}>
            inventory
          </Link>
          . From there you can put a weapon into the hands of one of your{" "}
          <Link to={UNITS_PATH} className={INLINE_LINK_CLASSES}>
            units
          </Link>
          , and it stays there. There is nowhere to take them yet: the battlefield appears in the
          navigation when it is ready to be used.
        </p>

        <FactList>
          <Fact label="Signed in as">{data?.account?.email}</Fact>
        </FactList>
      </div>
    </ShellScreen>
  );
}
