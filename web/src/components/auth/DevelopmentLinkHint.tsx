import { useEffect, useState } from "react";
import { Link } from "react-router";
import { safeRedirectTarget } from "@/auth/redirect";

const DEV_NOTIFICATIONS_URL = "/api/dev/account-notifications";

type NotificationKind = "EmailConfirmation" | "PasswordReset";

const ALLOWED_PATHS: Record<NotificationKind, string> = {
  EmailConfirmation: "/confirm-email",
  PasswordReset: "/reset-password",
};

/**
 * DEVELOPMENT ONLY. Surfaces the link a real email provider would have delivered.
 *
 * The endpoint behind it is mapped only when the API runs in the Development environment,
 * and the whole component is behind `import.meta.env.DEV` so the bundler drops it from a
 * production build. It changes nothing about what the account endpoints tell a caller — it
 * reads a local capture the server made of what it tried to send.
 */
export function DevelopmentLinkHint({ kind }: { kind: NotificationKind }) {
  const [path, setPath] = useState<string | null>(null);

  useEffect(() => {
    if (!import.meta.env.DEV) {
      return;
    }

    const controller = new AbortController();

    void (async () => {
      try {
        const response = await fetch(DEV_NOTIFICATIONS_URL, { signal: controller.signal });
        if (!response.ok) {
          return;
        }

        const captured: unknown = await response.json();
        if (!Array.isArray(captured)) {
          return;
        }

        const match = captured.find(
          (entry): entry is { kind: string; link: string } =>
            typeof entry === "object" &&
            entry !== null &&
            (entry as { kind?: unknown }).kind === kind &&
            typeof (entry as { link?: unknown }).link === "string",
        );

        if (!match) {
          return;
        }

        // The captured link is an absolute URL. Only its in-app path is used, and only when
        // it is the path this flow expects, so a stray value cannot become a redirect.
        const url = new URL(match.link, window.location.origin);
        const target = safeRedirectTarget(`${url.pathname}${url.search}`);

        if (target?.startsWith(ALLOWED_PATHS[kind])) {
          setPath(target);
        }
      } catch {
        // The endpoint is absent unless the API is running in development. Nothing to show.
      }
    })();

    return () => controller.abort();
  }, [kind]);

  if (!path) {
    return null;
  }

  return (
    <aside className="border-l-2 border-rune bg-ink/80 py-3 pl-4 pr-3">
      <p className="font-hud text-hud font-semibold uppercase tracking-[0.14em] text-rune">
        Development only
      </p>
      <p className="mt-1 font-body text-body leading-relaxed text-bone-dim">
        No email provider is configured, so the link was captured locally instead of sent.
      </p>
      <Link
        to={path}
        className="mt-2 inline-block font-hud text-hud font-semibold uppercase tracking-[0.10em] text-bone transition-colors hover:text-selected focus:text-selected focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-selected"
      >
        Open the captured link
      </Link>
    </aside>
  );
}
