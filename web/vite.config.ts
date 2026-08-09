import { fileURLToPath, URL } from "node:url";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

// Art lives at the repository root, outside `web/`, because it is shared with
// the design track. Aliasing it keeps a single copy instead of a build step
// that mirrors files into `web/public`.
const artDir = fileURLToPath(new URL("../art", import.meta.url));

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@art": artDir,
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  server: {
    port: 1337,
    // Fail instead of drifting to the next free port: .claude/launch.json
    // hardcodes 1337 and would open the wrong page.
    strictPort: true,
    fs: { allow: [".."] },
  },
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
