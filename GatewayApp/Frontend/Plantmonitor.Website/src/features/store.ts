import { writable, type Writable } from "svelte/store";
import type { DeviceHealthState } from "~/services/GatewayAppApi";
import { PhotoTourPlantInfo } from './../services/GatewayAppApi';
import type { ImageToCut } from "./photoStitching/ImageToCut";

export const selectedDevice: Writable<DeviceHealthState | undefined> = writable(undefined);
export const allDevices: Writable<DeviceHealthState[]> = writable([]);
export const selectedPhotoTourPlantInfo: Writable<PhotoTourPlantInfo[] | undefined> = writable(undefined);
export const plantPolygonChanged: Writable<PhotoTourPlantInfo | undefined> = writable(undefined);
export const imageToCutChanged: Writable<ImageToCut | undefined> = writable(undefined);