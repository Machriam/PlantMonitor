import { writable, type Writable } from "svelte/store";
import type { DeviceHealthState } from "~/services/GatewayAppApi";

export const selectedDevice: Writable<DeviceHealthState | undefined> = writable(undefined);
export const allDevices: Writable<DeviceHealthState[]> = writable([]);
export const navigationChanged: Writable<string> = writable("");
