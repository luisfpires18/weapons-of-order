import { Route, Routes } from "react-router";
import { PlaceholderScreen } from "@/screens/PlaceholderScreen";
import { TitleScreen } from "@/screens/TitleScreen";

const placeholders = [
  { path: "/hub", name: "HUB" },
  { path: "/barracks", name: "BARRACKS" },
  { path: "/forge", name: "FORGE" },
  { path: "/arrange", name: "ARRANGE" },
  { path: "/dungeon", name: "DUNGEON" },
  { path: "/vault", name: "VAULT" },
  { path: "/ladder", name: "LADDER" },
];

export function App() {
  return (
    <Routes>
      <Route path="/" element={<TitleScreen />} />
      {placeholders.map(({ path, name }) => (
        <Route key={path} path={path} element={<PlaceholderScreen name={name} />} />
      ))}
    </Routes>
  );
}
