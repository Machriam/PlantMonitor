import { writable, type Writable } from "svelte/store";
import type { DeviceHealthState } from "~/services/GatewayAppApi";
import { PhotoTourPlantInfo } from './../services/GatewayAppApi';

export const selectedDevice: Writable<DeviceHealthState | undefined> = writable(undefined);
export const allDevices: Writable<DeviceHealthState[]> = writable([]);
export const selectedPhotoTourPlantInfo: Writable<PhotoTourPlantInfo | undefined> = writable(undefined);