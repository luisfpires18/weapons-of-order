export function PlaceholderScreen({ name }: { name: string }) {
  return (
    <main className="flex h-dvh w-full items-center justify-center">
      <h1 className="font-display text-screen font-semibold tracking-[0.06em] text-bone">{name}</h1>
    </main>
  );
}
