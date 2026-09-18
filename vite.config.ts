import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// https://vitejs.dev/config/
export default defineConfig({
  base: "./",
  build: { outDir: "dist-ui" },
  plugins: [react(), tailwindcss()],
  server: {
    port: 3000,
    host: "127.0.0.1",
  },
});
