import {defineConfig} from "vitest/config";
import {svelte} from "@sveltejs/vite-plugin-svelte";
import path from "path";

export default defineConfig({
    plugins: [svelte({hot: !process.env.VITEST})],
    resolve: {
        alias: {
            "~": path.resolve(__dirname, "./src"),
            "$app/environment": path.resolve(__dirname, "./src/tests/mocks/environment.ts")
        }
    },
    test: {
        environment: "jsdom",
        setupFiles: ["src/types/typeExtensions.d.ts"],
        globals: true
    }
});
