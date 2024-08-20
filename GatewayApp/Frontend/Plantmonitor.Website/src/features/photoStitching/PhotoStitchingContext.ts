import { writable, type Writable } from "svelte/store";
import type { PhotoTourPlantInfo } from "~/services/GatewayAppApi";
import type { ImageToCut } from "./ImageToCut";

export const selectedPhotoTourPlantInfo: Writable<PhotoTourPlantInfo[] | undefined> = writable(undefined);
export const plantPolygonChanged: Writable<PhotoTourPlantInfo | undefined> = writable(undefined);
export const imageToCutChanged: Writable<ImageToCut | undefined> = writable(undefined);