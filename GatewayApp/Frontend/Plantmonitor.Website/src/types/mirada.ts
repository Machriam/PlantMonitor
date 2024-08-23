/* eslint-disable @typescript-eslint/no-explicit-any */
import type { Mat } from "mirada";

export class OptionalCvFunctions {
    applyColorMap(src: Mat, dst: Mat, colormap: ColormapTypes): void {
        (cv as any).applyColorMap(src, dst, colormap);
    }
}
export enum ColormapTypes {
    COLORMAP_AUTUMN = 0,
    COLORMAP_BONE = 1,
    COLORMAP_JET = 2,
    COLORMAP_WINTER = 3,
    COLORMAP_RAINBOW = 4,
    COLORMAP_OCEAN = 5,
    COLORMAP_SUMMER = 6,
    COLORMAP_SPRING = 7,
    COLORMAP_COOL = 8,
    COLORMAP_HSV = 9,
    COLORMAP_PINK = 10,
    COLORMAP_HOT = 11,
    COLORMAP_PARULA = 12,
    COLORMAP_MAGMA = 13,
    COLORMAP_INFERNO = 14,
    COLORMAP_PLASMA = 15,
    COLORMAP_VIRIDIS = 16,
    COLORMAP_CIVIDIS = 17,
    COLORMAP_TWILIGHT = 18,
    COLORMAP_TWILIGHT_SHIFTED = 19,
    COLORMAP_TURBO = 20,
    COLORMAP_DEEPGREEN = 21
}