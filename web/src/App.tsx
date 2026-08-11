import { Route, Routes } from "react-router";
import { NotFoundScreen } from "@/screens/NotFoundScreen";
import { TitleScreen } from "@/screens/TitleScreen";

// Task 1 removed the pre-Browser-V1 placeholder routes (/hub, /barracks, /forge,
// /arrange, /dungeon, /vault, /ladder). They predated the approved architecture and
// were not replaced with invented screens: real destinations arrive with the tasks
// that own them (auth in Task 2, the authenticated shell in Task 3). Anything else
// resolves as not found rather than quietly becoming the menu structure.
export function App() {
  return (
    <Routes>
      <Route path="/" element={<TitleScreen />} />
      <Route path="*" element={<NotFoundScreen />} />
    </Routes>
  );
}
