import { useState } from "react";
import { useNavigate } from "react-router";
import forgeBackground from "@art/backgrounds/forge-16x9.png";
import { WORLD_PATH } from "@/auth/redirect";
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

  // ENTER WORLD points at the destination itself, not at the sign-in screen. The route
  // guard is what turns a visitor without a session towards /login, so the same action
  // works for both states and a signed-in player is not asked to sign in again.
  const items: TitleMenuItem[] = [
    { label: "ENTER WORLD", onSelect: () => void navigate(WORLD_PATH) },
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
