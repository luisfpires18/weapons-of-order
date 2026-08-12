import type { ReactNode } from "react";
import { ApiProblem } from "@/api/problem";
import { FormNotice } from "@/components/auth/FormControls";
import { QuietAction } from "@/forge/ForgeActions";
import { ShellScreen } from "@/shell/ShellScreen";

/**
 * The two states every preparation screen needs before it has anything to show.
 *
 * Shared because the inventory and the units screen fail the same way and should say so in
 * the same voice — and because a screen whose loading state is an afterthought is how layout
 * jumps get shipped.
 */
export function ScreenPending({ title, children }: { title: string; children: string }) {
  return (
    <ShellScreen title={title}>
      <p role="status" className="font-body text-body text-bone-dim">
        {children}
      </p>
    </ShellScreen>
  );
}

export function ScreenError({
  title,
  error,
  fallback,
  onRetry,
}: {
  title: string;
  error: unknown;
  fallback: string;
  onRetry: () => void;
}) {
  return (
    <ShellScreen title={title}>
      <div className="flex max-w-[36rem] flex-col gap-6">
        <FormNotice tone="error">{error instanceof ApiProblem ? error.message : fallback}</FormNotice>
        <QuietAction onClick={onRetry}>Try again</QuietAction>
      </div>
    </ShellScreen>
  );
}

/**
 * A small label above a block of content, in the same register the forge rail uses. Not a
 * card heading: there is no card.
 */
export function SectionLabel({ children }: { children: ReactNode }) {
  return (
    <h2 className="font-hud text-hud font-semibold uppercase tracking-[0.16em] text-bone-dim">
      {children}
    </h2>
  );
}
