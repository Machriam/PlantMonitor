import adapter from "@sveltejs/adapter-static";
import {vitePreprocess} from "@sveltejs/vite-plugin-svelte";

/** @type {import('@sveltejs/kit').Config} */
const config = {
    preprocess: vitePreprocess(),
    compilerOptions: {customElement: true},

    kit: {
        alias: {
            "~": "./src",
            typeExtensions: "./src/types/TypeExtensions.d.ts"
        },
        files: {
            routes: "src/features"
        },
        adapter: adapter({
            pages: "build",
            assets: "build",
            fallback: undefined,
            precompress: false,
            strict: true
        })
    }
};

export default config;
