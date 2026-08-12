import { useCallback, useRef } from "react";
import type { PointerEvent as ReactPointerEvent, KeyboardEvent as ReactKeyboardEvent } from "react";

/**
 * The two controls at the anvil.
 *
 * Both are ordinary buttons. Nothing here is an image, a sprite or a canvas, so they size
 * with their text, take focus visibly, and answer a keyboard the same way they answer a
 * thumb — which is the point of the rule that core controls stay HTML.
 */

const CONTROL =
  "flex min-h-16 flex-1 cursor-pointer select-none items-center justify-center rounded-panel" +
  " border-[length:var(--border-panel)] px-4 font-hud text-[0.9375rem] font-semibold uppercase" +
  " tracking-[0.18em] transition-colors focus-visible:outline-2 focus-visible:outline-offset-4" +
  " motion-reduce:transition-none disabled:cursor-not-allowed lg:min-h-20 lg:text-[1.0625rem]";

/**
 * Hold to put the workpiece in the fire; let go to take it out.
 *
 * A press-and-hold rather than a click counter: heating is a state the player is in, not an
 * event they repeat, and asking anyone to hammer a button to raise a temperature would be a
 * worse game on a mouse and an unusable one on a phone.
 *
 * Pointer events cover mouse, pen and touch in one path, and the pointer is captured so a
 * finger that slides off the control still counts as holding — releasing is what stops it.
 * Space and Enter hold in the same way, with the browser's key repeat filtered out so a held
 * key is one press rather than a stream of them.
 */
export function HoldToHeat({
  heating,
  disabled,
  onChange,
}: {
  heating: boolean;
  disabled: boolean;
  onChange: (heating: boolean) => void;
}) {
  const held = useRef(false);

  const start = useCallback(() => {
    if (held.current) return;
    held.current = true;
    onChange(true);
  }, [onChange]);

  const stop = useCallback(() => {
    if (!held.current) return;
    held.current = false;
    onChange(false);
  }, [onChange]);

  const capture = (event: ReactPointerEvent<HTMLButtonElement>) => {
    // Without this the browser scrolls the page on a touch drag instead of holding the
    // control, and the player loses the fire mid-heat.
    event.preventDefault();
    // Capture is what lets a finger slide off the control and still count as holding. It is
    // not load-bearing: a pointer the browser no longer considers active throws here, and a
    // headless DOM may not implement it at all. Either way the hold itself still works.
    try {
      event.currentTarget.setPointerCapture(event.pointerId);
    } catch {
      // No capture. Releasing is still what stops the fire.
    }

    start();
  };

  const key = (event: ReactKeyboardEvent<HTMLButtonElement>, down: boolean) => {
    if (event.key !== " " && event.key !== "Enter") return;

    event.preventDefault();
    if (down) {
      start();
    } else {
      stop();
    }
  };

  return (
    <button
      type="button"
      disabled={disabled}
      aria-pressed={heating}
      onPointerDown={capture}
      onPointerUp={stop}
      onPointerCancel={stop}
      onLostPointerCapture={stop}
      onKeyDown={(event) => key(event, true)}
      onKeyUp={(event) => key(event, false)}
      onBlur={stop}
      style={{ touchAction: "none" }}
      className={`${CONTROL} focus-visible:outline-ember-bright ${
        heating
          ? "border-ember bg-ember text-void"
          : "border-slate bg-ink/70 text-bone hover:border-ember hover:text-ember-bright"
      } disabled:border-slate/60 disabled:bg-transparent disabled:text-bone-dim/60 disabled:hover:border-slate/60`}
    >
      {heating ? "In the fire" : "Hold to heat"}
    </button>
  );
}

/**
 * One hammer. Canon asks for a single Strike action rather than a bank of attack buttons,
 * and a forge that needs only a few meaningful blows.
 */
export function Strike({ disabled, onStrike }: { disabled: boolean; onStrike: () => void }) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onStrike}
      style={{ touchAction: "manipulation" }}
      className={`${CONTROL} border-ember bg-transparent text-ember-bright focus-visible:outline-ember-bright hover:bg-ember hover:text-void active:bg-ember-bright active:text-void disabled:border-slate/60 disabled:text-bone-dim/60 disabled:hover:bg-transparent disabled:hover:text-bone-dim/60`}
    >
      Strike
    </button>
  );
}

/**
 * The action that starts or restarts a piece of work: putting iron on the anvil, or clearing
 * what is left of the last attempt. Sized like the anvil controls so the screen keeps one
 * button scale throughout.
 */
export function AnvilAction({
  children,
  pending,
  pendingLabel,
  disabled,
  onClick,
}: {
  children: string;
  pending: boolean;
  pendingLabel: string;
  disabled?: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      disabled={pending || disabled}
      aria-busy={pending}
      onClick={onClick}
      className={`${CONTROL} max-w-[26rem] grow-0 border-ember bg-transparent text-ember-bright focus-visible:outline-ember-bright hover:bg-ember hover:text-void disabled:border-slate/60 disabled:text-bone-dim/60 disabled:hover:bg-transparent disabled:hover:text-bone-dim/60`}
    >
      {pending ? pendingLabel : children}
    </button>
  );
}

/** A standalone quiet action, in the same register as the title-screen menu. */
export function QuietAction({ children, onClick }: { children: string; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="cursor-pointer self-start font-hud text-hud font-semibold uppercase tracking-[0.1em] text-bone-dim transition-colors hover:text-selected focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-selected motion-reduce:transition-none"
    >
      {children}
    </button>
  );
}
