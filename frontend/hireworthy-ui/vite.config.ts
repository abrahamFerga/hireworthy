import { fileURLToPath } from "node:url";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Hireworthy's app entry. @plenipo/ui installs from the PUBLIC npm registry (ADR-0011, OQ9
// resolved) — no .npmrc, no token, so a bare clone builds and CI stays keyless.
//
// The published @plenipo/ui dist bakes VITE_API_BASE="" at library build time, and that empty base
// cannot be moved from here. So the shell asks for /api and /hubs same-origin — which on this dev
// server is Vite, not the API. Proxy them, or every request 404s and the shell renders
// "Can't reach the Plenipo API".
const apiTarget = process.env.VITE_API_TARGET ?? "http://localhost:5000";

export default defineConfig(() => {
  process.env.VITE_BRAND_NAME ??= "Hireworthy";

  return {
    plugins: [react()],
    build: { outDir: fileURLToPath(new URL("dist", import.meta.url)) },
    server: {
      proxy: {
        "/api": { target: apiTarget, changeOrigin: true },
        "/hubs": { target: apiTarget, changeOrigin: true, ws: true },
      },
    },
  };
});
