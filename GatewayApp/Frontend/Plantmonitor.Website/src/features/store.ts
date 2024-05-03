import { writable, type Writable } from "svelte/store";
import type { DeviceHealthState } from "~/services/GatewayAppApi";

export const selectedDevice: Writable<DeviceHealthState | undefined> = writable(undefined);