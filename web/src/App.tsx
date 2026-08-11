import { Route, Routes } from "react-router";
import { WORLD_PATH } from "@/auth/redirect";
import { RequireAnonymous, RequireAuth } from "@/auth/RouteGuards";
import { NotFoundScreen } from "@/screens/NotFoundScreen";
import { TitleScreen } from "@/screens/TitleScreen";
import { WorldEntryScreen } from "@/screens/WorldEntryScreen";
import { ConfirmEmailScreen } from "@/screens/auth/ConfirmEmailScreen";
import { ForgotPasswordScreen } from "@/screens/auth/ForgotPasswordScreen";
import { LoginScreen } from "@/screens/auth/LoginScreen";
import { RegisterScreen } from "@/screens/auth/RegisterScreen";
import { ResetPasswordScreen } from "@/screens/auth/ResetPasswordScreen";

// Task 1 removed the pre-Browser-V1 placeholder routes (/hub, /barracks, /forge,
// /arrange, /dungeon, /vault, /ladder) and Task 2 does not bring any of them back: an old
// URL resolves as not found rather than quietly becoming a way past the sign-in screens.
// Real game destinations arrive with the authenticated shell in Task 3.
export function App() {
  return (
    <Routes>
      <Route path="/" element={<TitleScreen />} />

      {/* Signing in from one of these would leave the player looking at a form they have
          already completed, so a live session sends them on to the world instead. */}
      <Route element={<RequireAnonymous />}>
        <Route path="/login" element={<LoginScreen />} />
        <Route path="/register" element={<RegisterScreen />} />
        <Route path="/forgot-password" element={<ForgotPasswordScreen />} />
      </Route>

      {/* Reachable either way: these are followed from an email, and being signed in
          already is not a reason to refuse a reset or a confirmation. */}
      <Route path="/reset-password" element={<ResetPasswordScreen />} />
      <Route path="/confirm-email" element={<ConfirmEmailScreen />} />

      <Route element={<RequireAuth />}>
        <Route path={WORLD_PATH} element={<WorldEntryScreen />} />
      </Route>

      <Route path="*" element={<NotFoundScreen />} />
    </Routes>
  );
}
