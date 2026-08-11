import { useState } from "react";
import { useNavigate } from "react-router";
import forgeBackground from "@art/backgrounds/forge-16x9.png";
import { SettingsPanel } from "@/components/SettingsPanel";
import { TitleMenu } from "@/components/TitleMenu";
import type { TitleMenuItem } from "@/components/TitleMenu";
import { Wordmark } from "@/components/Wordmark";

const SCRIM =
  "linear-gradient(to bottom," +
  " color-mix(in srgb, var(--color-void) 70%, transparent) 0%," +
  " color-mix(in srgb, var(--color-void) 30%, transparent) 60%," +
  " color-mix(in srgb, var(--color-void) 30%, transparent) 100%)";

export function TitleScreen() {
  const navigate = useNavigate();
  const [settingsOpen, setSettingsOpen] = useState(false);

  // ENTER WORLD points at its real future destination. Task 2 implements /login and
  // the authenticated session; until then it resolves to the not-found screen rather
  // than to the removed /hub placeholder or to a faked completed login.
  const items: TitleMenuItem[] = [
    { label: "ENTER WORLD", onSelect: () => void navigate("/login") },
    { label: "SETTINGS", onSelect: () => setSettingsOpen(true) },
  ];

  return (
    <main className="relative h-dvh w-full overflow-hidden">
      <img
        src={forgeBackground}
        alt=""
        aria-hidden
        className="absolute inset-0 h-full w-full object-cover"
      />
      <div aria-hidden className="absolute inset-0" style={{ background: SCRIM }} />

      <div className="relative z-10 flex h-full flex-col items-center justify-center gap-16">
        <Wordmark />
        <TitleMenu items={items} />
      </div>

      {settingsOpen && <SettingsPanel onClose={() => setSettingsOpen(false)} />}
    </main>
  );
}
