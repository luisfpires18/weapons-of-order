// The rules flanking OF are centred with `items-center` on a line box of
// `line-height: 1`. For Cinzel that line-box centre sits within 0.01em of the
// cap-height centre, so no manual nudge is needed.
export function Wordmark() {
  return (
    <h1
      aria-label="Weapons of Order"
      className="flex select-none flex-col items-center font-display font-bold text-bone"
    >
      {/* Tracking sits on each line, not the h1: 0.06em has to resolve against
          that line's own size. letter-spacing also adds a trailing space after
          the last glyph, which the negative margin removes so the line
          optically centres. */}
      <span aria-hidden className="text-title leading-none tracking-[0.06em] mr-[-0.06em]">
        WEAPONS
      </span>

      <span
        aria-hidden
        className="flex items-center gap-[0.5em] text-[calc(var(--text-title)*0.55)] leading-none tracking-[0.06em]"
      >
        <span className="h-[2px] w-[1.5em] bg-ember" />
        <span className="mr-[-0.06em]">OF</span>
        <span className="h-[2px] w-[1.5em] bg-ember" />
      </span>

      <span aria-hidden className="text-title leading-none tracking-[0.06em] mr-[-0.06em]">
        ORDER
      </span>
    </h1>
  );
}
