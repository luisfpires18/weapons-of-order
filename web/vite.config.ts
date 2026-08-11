import { fileURLToPath, URL } from "node:url";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { VitePWA } from "vite-plugin-pwa";
import { defineConfig } from "vitest/config";

// Art lives at the repository root, outside `web/`, because it is shared with
// the design track. Aliasing it keeps a single copy instead of a build step
// that mirrors files into `web/public`.
const artDir = fileURLToPath(new URL("../art", import.meta.url));

// The ASP.NET Core host in `server/`. See its Properties/launchSettings.json.
const apiTarget = "http://localhost:5180";

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      registerType: "autoUpdate",
      includeAssets: ["favicon.svg", "icons/apple-touch-icon-180.png"],
      manifest: {
        id: "/",
        name: "Weapons of Order",
        short_name: "Weapons of Order",
        description: "Forging-centered fantasy autobattler.",
        start_url: "/",
        scope: "/",
        display: "standalone",
        background_color: "#030A10",
        theme_color: "#030A10",
        icons: [
          { src: "/icons/icon-192.png", sizes: "192x192", type: "image/png", purpose: "any" },
          { src: "/icons/icon-512.png", sizes: "512x512", type: "image/png", purpose: "any" },
          {
            src: "/icons/icon-maskable-512.png",
            sizes: "512x512",
            type: "image/png",
            purpose: "maskable",
          },
        ],
      },
      workbox: {
        // Precache the versioned static shell only. Browser V1 is an online game:
        // there is deliberately no runtimeCaching entry, so no API response — least
        // of all an authenticated one — is ever stored by the service worker.
        globPatterns: ["**/*.{js,css,html,svg,png,woff,woff2}"],
        runtimeCaching: [],
        navigateFallback: "index.html",
        // A navigation to /api/... must reach the server, not the cached shell.
        navigateFallbackDenylist: [/^\/api\//],
        cleanupOutdatedCaches: true,
      },
      devOptions: {
        // The service worker is a production concern; leaving it off in `vite dev`
        // avoids stale-cache confusion during development.
        enabled: false,
      },
    }),
  ],
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
    proxy: {
      // Keeps the browser on one origin in development, matching the deployed
      // topology where ASP.NET Core serves both the client and /api. No CORS,
      // no cross-site cookie rules to work around when auth lands in Task 2.
      "/api": { target: apiTarget },
    },
  },
  test: {
    // Node by default: most tests here are pure functions and fetch contracts, and a DOM
    // they never touch is only startup cost. The shell tests that do need one opt in with
    // an `@vitest-environment jsdom` docblock.
    environment: "node",
    include: ["src/**/*.test.{ts,tsx}"],
  },
});
