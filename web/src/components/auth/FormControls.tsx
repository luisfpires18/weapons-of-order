import type { ReactNode } from "react";

/**
 * A ruled field rather than a boxed one: dark ink, a slate baseline, and an ember edge when
 * it takes focus. Text is set at 1rem so mobile Safari does not zoom the viewport on focus,
 * and the control is 3rem tall so it is a comfortable touch target.
 */
const INPUT_CLASSES =
  "min-h-12 w-full rounded-none border-0 border-b-2 border-slate bg-ink/70 px-3 py-2 font-body text-[1rem] text-bone transition-colors" +
  " placeholder:text-bone-dim/60 hover:border-bone-dim focus:border-ember" +
  " focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ember" +
  " aria-[invalid=true]:border-danger";

export function TextField({
  id,
  label,
  type,
  value,
  onChange,
  autoComplete,
  hint,
  error,
  required = true,
}: {
  id: string;
  label: string;
  type: "email" | "password" | "text";
  value: string;
  onChange: (value: string) => void;
  autoComplete: string;
  hint?: string;
  error?: string;
  required?: boolean;
}) {
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const describedBy = [errorId, hintId].filter(Boolean).join(" ") || undefined;

  return (
    <div className="flex flex-col gap-2">
      <label
        htmlFor={id}
        className="font-hud text-hud font-semibold uppercase tracking-[0.16em] text-bone-dim"
      >
        {label}
      </label>
      <input
        id={id}
        name={id}
        type={type}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        autoComplete={autoComplete}
        required={required}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        className={INPUT_CLASSES}
      />
      {hint ? (
        <p id={hintId} className="font-body text-body text-bone-dim">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={errorId} className="font-body text-body text-ember-bright">
          {error}
        </p>
      ) : null}
    </div>
  );
}

/**
 * Drawn rather than inherited: the platform checkbox is a white square, which on this
 * palette is the brightest thing on the screen and pulls attention off the primary action.
 * Inlined as a data URI because an external asset would be blocked from the same-origin
 * bundle for no benefit at this size.
 */
const CHECK_MARK =
  "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'" +
  " fill='none' stroke='%23030A10' stroke-width='2.5' stroke-linecap='round'" +
  " stroke-linejoin='round'%3E%3Cpath d='M3.5 8.5 6.5 11.5 12.5 4.5'/%3E%3C/svg%3E\")";

export function Checkbox({
  id,
  label,
  checked,
  onChange,
}: {
  id: string;
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
}) {
  return (
    // The label wraps the control so the whole 3rem row is one activation area. A 24px box
    // on its own is an uncomfortable tap target; the row is not.
    <label
      htmlFor={id}
      className="flex min-h-12 cursor-pointer items-center gap-3 font-body text-body text-bone-dim"
    >
      <input
        id={id}
        name={id}
        type="checkbox"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
        className="size-6 shrink-0 cursor-pointer appearance-none rounded-panel border-[length:var(--border-panel)] border-slate bg-ink bg-center bg-no-repeat transition-colors hover:border-bone-dim checked:border-ember checked:bg-ember focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-selected"
        style={{
          backgroundImage: checked ? CHECK_MARK : undefined,
          backgroundSize: "1rem",
        }}
      />
      <span>{label}</span>
    </label>
  );
}

export function PrimaryAction({
  children,
  pending,
  pendingLabel,
}: {
  children: ReactNode;
  pending: boolean;
  pendingLabel: string;
}) {
  return (
    <button
      type="submit"
      disabled={pending}
      aria-busy={pending}
      className="min-h-12 w-full cursor-pointer rounded-panel border-[length:var(--border-panel)] border-ember bg-transparent px-6 font-hud text-[0.9375rem] font-semibold uppercase tracking-[0.18em] text-ember-bright transition-colors hover:bg-ember hover:text-void focus-visible:bg-ember focus-visible:text-void focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ember-bright disabled:cursor-progress disabled:border-slate disabled:bg-transparent disabled:text-bone-dim"
    >
      {pending ? pendingLabel : children}
    </button>
  );
}

/** A standalone quiet action, in the same register as the title-screen menu. */
export const QUIET_ACTION_CLASSES =
  "cursor-pointer font-hud text-hud font-semibold uppercase tracking-[0.10em] text-bone-dim transition-colors" +
  " hover:text-selected focus:text-selected focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-selected";

/**
 * A link sitting inside a sentence. It stays at body size and weight so the line still
 * reads as prose; the ember underline is what marks it as the action.
 */
export const INLINE_LINK_CLASSES =
  "cursor-pointer font-body text-body text-bone underline decoration-ember decoration-2 underline-offset-4 transition-colors" +
  " hover:text-ember-bright focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-selected";

/**
 * A form-level message. `alert` for failures so a screen reader is interrupted; `status`
 * for confirmations, which should not interrupt what the reader is already saying.
 */
export function FormNotice({ tone, children }: { tone: "error" | "info"; children: ReactNode }) {
  const accent = tone === "error" ? "border-danger" : "border-rune";

  return (
    <p
      role={tone === "error" ? "alert" : "status"}
      className={`border-l-2 ${accent} bg-ink/70 py-3 pl-4 pr-3 font-body text-body leading-relaxed text-bone`}
    >
      {children}
    </p>
  );
}
