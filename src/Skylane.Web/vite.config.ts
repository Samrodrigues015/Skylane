import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Em desenvolvimento, o Vite corre na porta 5173 e reencaminha /api para a API .NET (5080).
// No build, os ficheiros vão para o wwwroot da API, que os serve em produção (e no Docker).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": "http://localhost:5080",
    },
  },
  build: {
    outDir: "../Skylane.Api/wwwroot",
    emptyOutDir: true,
  },
});
