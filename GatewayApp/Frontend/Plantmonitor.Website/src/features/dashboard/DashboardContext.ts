import { writable, type Writable } from "svelte/store";
import type { PhotoTourInfo, SegmentationParameter } from "~/services/GatewayAppApi";

export const _virtualImageFilterByTime: Writable<Set<number>> = writable(new Set());
export const _selectedTourChanged: Writable<PhotoTourInfo | null> = writable(null);
export const _segmentationChanged: Writable<SegmentationParameter[]> = writable([]);


