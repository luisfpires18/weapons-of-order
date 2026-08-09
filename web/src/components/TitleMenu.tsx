import { useRef } from "react";

export type TitleMenuItem = {
  label: string;
  onSelect: () => void;
};

export function TitleMenu({ items }: { items: TitleMenuItem[] }) {
  const buttons = useRef<(HTMLButtonElement | null)[]>([]);

  const onKeyDown = (event: React.KeyboardEvent<HTMLElement>) => {
    const delta =
      event.key === "ArrowDown" || event.key === "ArrowRight"
        ? 1
        : event.key === "ArrowUp" || event.key === "ArrowLeft"
          ? -1
          : 0;
    if (delta === 0) return;
    event.preventDefault();

    // The current position is read from the DOM, not from state: two keydowns
    // inside one task would otherwise both see the same pre-render value.
    const current = buttons.current.indexOf(document.activeElement as HTMLButtonElement);
    const next =
      current === -1
        ? delta > 0
          ? 0
          : items.length - 1
        : (current + delta + items.length) % items.length;
    buttons.current[next]?.focus();
  };

  return (
    <nav aria-label="Main menu" onKeyDown={onKeyDown}>
      <ul className="flex flex-col items-center gap-6">
        {items.map((item, index) => (
          <li key={item.label}>
            <button
              type="button"
              ref={(node) => {
                buttons.current[index] = node;
              }}
              onClick={item.onSelect}
              className="cursor-pointer font-display text-menu font-semibold tracking-[0.10em] text-bone transition-colors hover:text-selected focus:text-selected focus-visible:outline-2 focus-visible:outline-offset-6 focus-visible:outline-selected"
            >
              {item.label}
            </button>
          </li>
        ))}
      </ul>
    </nav>
  );
}
