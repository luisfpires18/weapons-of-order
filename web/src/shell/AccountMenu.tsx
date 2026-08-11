import { useEffect, useRef, useState } from "react";
import { Link } from "react-router";
import { ApiProblem } from "@/api/problem";
import { useSession } from "@/auth/useSession";
import { ACCOUNT_PATH } from "@/shell/destinations";
import { useSignOut } from "@/shell/useSignOut";

/**
 * The account control in the desktop top bar.
 *
 * A disclosure, not a `role="menu"` widget. What it opens is a link and a button, which the
 * browser already knows how to operate with Tab and Enter; claiming menu semantics would
 * promise arrow-key navigation and typeahead that a two-item panel does not need and that
 * would have to be hand-built to be honest.
 *
 * It is desktop-only on purpose. The phone reaches the same two things through the bottom
 * bar and the Account screen, which are full-size targets — better than a popover anchored
 * to a small control in a compact header.
 */
export function AccountMenu() {
  const { data } = useSession();
  const signOut = useSignOut();

  const [open, setOpen] = useState(false);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const firstItemRef = useRef<HTMLAnchorElement>(null);

  useEffect(() => {
    if (!open) return;

    firstItemRef.current?.focus();

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;
      setOpen(false);
      // Focus goes back to what opened the panel, or it would land on <body> and the next
      // Tab would start over from the top of the page.
      triggerRef.current?.focus();
    };

    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node;
      if (panelRef.current?.contains(target) || triggerRef.current?.contains(target)) return;
      setOpen(false);
    };

    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("pointerdown", onPointerDown);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("pointerdown", onPointerDown);
    };
  }, [open]);

  const email = data?.account?.email ?? "";
  const problem = signOut.error instanceof ApiProblem ? signOut.error : null;

  return (
    <div className="relative hidden lg:block">
      <button
        ref={triggerRef}
        type="button"
        aria-expanded={open}
        aria-controls="account-menu-panel"
        onClick={() => setOpen((wasOpen) => !wasOpen)}
        className="flex min-h-10 max-w-[20rem] cursor-pointer items-center gap-3 border-[length:var(--border-panel)] border-transparent px-3 font-hud text-[0.8125rem] font-semibold uppercase tracking-[0.14em] text-bone-dim transition-colors hover:border-slate hover:text-bone aria-expanded:border-slate aria-expanded:text-bone focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-selected motion-reduce:transition-none"
      >
        <span className="truncate normal-case tracking-[0.04em]">{email}</span>
        <Chevron open={open} />
      </button>

      {open ? (
        <div
          id="account-menu-panel"
          ref={panelRef}
          className="absolute right-0 top-[calc(100%+0.5rem)] z-40 flex w-[19rem] flex-col rounded-panel border-[length:var(--border-panel)] border-slate bg-ink shadow-[0_18px_40px_rgba(3,10,16,0.65)]"
        >
          <p className="border-b-[length:var(--border-panel)] border-slate/70 px-5 py-4 font-body text-body break-all text-bone-dim">
            <span className="mb-1 block font-hud text-hud font-semibold uppercase tracking-[0.16em] text-bone-dim">
              Signed in as
            </span>
            <span className="text-bone">{email}</span>
          </p>

          {/* Closed here rather than on a route change, because this is the only navigation
              that leaves the panel open: a click anywhere else is already an outside click. */}
          <Link
            ref={firstItemRef}
            to={ACCOUNT_PATH}
            onClick={() => setOpen(false)}
            className={ITEM_CLASSES}
          >
            Account
          </Link>

          <button
            type="button"
            disabled={signOut.isPending}
            aria-busy={signOut.isPending}
            onClick={() => signOut.mutate()}
            className={`${ITEM_CLASSES} cursor-pointer text-left text-ember-bright hover:bg-ember hover:text-void focus-visible:bg-ember focus-visible:text-void disabled:cursor-progress disabled:bg-transparent disabled:text-bone-dim`}
          >
            {signOut.isPending ? "Signing out" : "Sign out"}
          </button>

          {problem ? (
            <p role="alert" className="border-t-[length:var(--border-panel)] border-danger px-5 py-3 font-body text-body text-bone">
              {problem.message}
            </p>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

const ITEM_CLASSES =
  "flex min-h-12 items-center px-5 font-hud text-[0.8125rem] font-semibold uppercase tracking-[0.16em] text-bone transition-colors hover:bg-ink-raised focus-visible:bg-ink-raised focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-selected motion-reduce:transition-none";

function Chevron({ open }: { open: boolean }) {
  return (
    <svg
      aria-hidden
      viewBox="0 0 10 6"
      className={`h-[6px] w-[10px] shrink-0 transition-transform motion-reduce:transition-none ${open ? "rotate-180" : ""}`}
    >
      <path d="M1 1l4 4 4-4" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="square" />
    </svg>
  );
}
