import { writable, type Writable } from "svelte/store";

export const _virtualImageFilterByTime: Writable<Set<number>> = writable(new Set());