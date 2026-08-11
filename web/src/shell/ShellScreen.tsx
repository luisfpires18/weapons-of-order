import type { ReactNode } from "react";
import { EmberRule } from "@/components/EmberRule";

/**
 * The content frame of an authenticated screen: identity, the ember rule that marks a
 * Weapons of Order heading, then the screen's own work.
 *
 * Deliberately not a card. The title screen establishes that a surface does not need a box
 * around it, and the room this leaves is what the forge, inventory grids and deployment
 * views are going to want. The width cap is generous rather than absent so a line of prose
 * on an ultrawide display still ends somewhere a reader can find.
 */
export function ShellScreen({
  title,
  lead,
  children,
}: {
  title: string;
  lead?: string;
  children?: ReactNode;
}) {
  return (
    <div className="mx-auto flex w-full max-w-[96rem] flex-col gap-10 px-[max(1.5rem,env(safe-area-inset-left))] py-10 lg:gap-12 lg:px-12 lg:py-14">
      {/* A plain div, not a <header>: the shell already contributes the page's banner, and a
          second one competes with it in the landmark list for no gain. */}
      <div className="flex max-w-[44rem] flex-col gap-4">
        <h1 className="font-display text-[1.75rem] font-semibold uppercase leading-tight tracking-[0.12em] text-bone lg:text-[2.25rem]">
          {title}
        </h1>
        <EmberRule />
        {lead ? <p className="font-body text-[1rem] leading-relaxed text-bone-dim">{lead}</p> : null}
      </div>

      {children}
    </div>
  );
}

/**
 * A read-only fact about the account or the session. Label above value, no box: the same
 * label/value register the auth forms already use for their fields.
 */
export function FactList({ children }: { children: ReactNode }) {
  return <dl className="flex flex-col gap-6">{children}</dl>;
}

export function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-2">
      <dt className="font-hud text-hud font-semibold uppercase tracking-[0.16em] text-bone-dim">
        {label}
      </dt>
      <dd className="font-body text-[1rem] break-all text-bone">{children}</dd>
    </div>
  );
}
