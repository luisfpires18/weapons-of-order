import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router";
import { AUTH_URLS, postJson } from "@/auth/session";
import { clearPrivateState, useAntiforgeryTokens } from "@/auth/useSession";

/**
 * Signing out, in the one place both the account menu and the Account screen call.
 *
 * The server ends the session — this is a real antiforgery-protected POST, not the client
 * forgetting who it is. What happens on the client afterwards is cleanup, in this order:
 * every cached response is dropped, then the browser is sent to the public title screen with
 * `replace`, so the shell entry is gone from history rather than one Back press away.
 *
 * Back still reaches an earlier shell URL if one is in the history stack. That is fine and
 * deliberate: the cache is empty, so the route guard has to ask the server again, and the
 * server now answers that nobody is signed in. The player lands on the login screen without
 * a frame of the previous account's screen being drawn.
 */
export function useSignOut() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const tokens = useAntiforgeryTokens();

  return useMutation({
    mutationFn: () => postJson(AUTH_URLS.logout, {}, tokens),
    onSuccess: () => {
      clearPrivateState(queryClient);
      void navigate("/", { replace: true });
    },
  });
}
