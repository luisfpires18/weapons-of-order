import { useEffect } from "react";

export function SettingsPanel({ onClose }: { onClose: () => void }) {
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onClose]);

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label="Settings"
      className="absolute inset-0 z-20 flex items-center justify-center bg-void/80"
    >
      {/* --border-panel is not in a Tailwind namespace, so the width has to be
          referenced as an arbitrary value rather than a `border-*` utility. */}
      <div className="flex w-[min(28rem,90vw)] flex-col gap-6 rounded-panel border-[length:var(--border-panel)] border-slate bg-ink p-8">
        <h2 className="font-display text-screen font-semibold tracking-[0.06em] text-bone">
          SETTINGS
        </h2>
        <p className="font-body text-body text-bone-dim">Nothing to configure yet.</p>
        <button
          type="button"
          autoFocus
          onClick={onClose}
          className="cursor-pointer self-start font-hud text-hud font-semibold uppercase tracking-[0.08em] text-bone transition-colors hover:text-selected focus:text-selected focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-selected"
        >
          Close
        </button>
      </div>
    </div>
  );
}
