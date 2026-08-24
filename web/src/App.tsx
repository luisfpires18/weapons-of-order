import { Route, Routes } from "react-router";
import { WORLD_PATH } from "@/auth/redirect";
import { RequireAnonymous, RequireAuth } from "@/auth/RouteGuards";
import { AccountScreen } from "@/screens/AccountScreen";
import { BattleScreen } from "@/screens/BattleScreen";
import { ForgeScreen } from "@/screens/ForgeScreen";
import { InventoryScreen } from "@/screens/InventoryScreen";
import { NotFoundScreen } from "@/screens/NotFoundScreen";
import { TitleScreen } from "@/screens/TitleScreen";
import { UnitsScreen } from "@/screens/UnitsScreen";
import { WorldScreen } from "@/screens/WorldScreen";
import { ConfirmEmailScreen } from "@/screens/auth/ConfirmEmailScreen";
import { ForgotPasswordScreen } from "@/screens/auth/ForgotPasswordScreen";
import { LoginScreen } from "@/screens/auth/LoginScreen";
import { RegisterScreen } from "@/screens/auth/RegisterScreen";
import { ResetPasswordScreen } from "@/screens/auth/ResetPasswordScreen";
import { ACCOUNT_PATH, BATTLE_PATH, FORGE_PATH, INVENTORY_PATH, UNITS_PATH } from "@/shell/destinations";
import { ShellLayout } from "@/shell/ShellLayout";

// Task 1 removed the pre-Browser-V1 placeholder routes (/hub, /barracks, /arrange, /dungeon,
// /vault, /ladder), and nothing since has brought any of them back — not even as a redirect,
// because a redirect claims there is a current destination the old name meant, and there is
// not. They fall to the catch-all and resolve as not found.
//
// /forge was in that set until Task 4. It is a real destination now, and the route below is
// the actual forge rather than a revival of the placeholder that used to answer there.
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

      {/* The guard decides whether there is a session; the shell is what a session gets.
          Nesting them this way is what let Tasks 4-6 add a screen by adding one line here
          and one destination to the navigation, with no change to either. */}
      <Route element={<RequireAuth />}>
        <Route element={<ShellLayout />}>
          <Route path={WORLD_PATH} element={<WorldScreen />} />
          <Route path={FORGE_PATH} element={<ForgeScreen />} />
          <Route path={INVENTORY_PATH} element={<InventoryScreen />} />
          <Route path={UNITS_PATH} element={<UnitsScreen />} />
          <Route path={BATTLE_PATH} element={<BattleScreen />} />
          <Route path={ACCOUNT_PATH} element={<AccountScreen />} />
        </Route>
      </Route>

      <Route path="*" element={<NotFoundScreen />} />
    </Routes>
  );
}
