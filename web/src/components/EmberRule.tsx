/**
 * The recurring mark of every Weapons of Order heading: a bar of ember cooling to nothing
 * along its length. It answers the rules flanking OF in the wordmark, and the same colour is
 * what a field border turns when it takes focus and what the shell's navigation rail lights
 * at the current destination.
 */
export function EmberRule() {
  return (
    <span
      aria-hidden
      className="block h-[2px] w-full"
      style={{
        background:
          "linear-gradient(to right, var(--color-ember) 0%," +
          " color-mix(in srgb, var(--color-ember) 35%, transparent) 45%, transparent 100%)",
      }}
    />
  );
}
