import { DeviceHealthState, AutomaticTourStartInfo as ImportedStartInfo } from "../../services/GatewayAppApi";

export const isValid = Symbol("isValid");

export function isValidImpl(this: ImportedStartInfo, device: DeviceHealthState | undefined) {
    if (this.name?.isEmpty()) return false;
    if (this.intervallInMinutes < 0.5) return false;
    if (device == undefined || device.health.deviceId == undefined) return false;
    if (this.movementPlan <= 0) return false;
    return true;
}
declare module "../../services/GatewayAppApi" {
    interface AutomaticTourStartInfo {
        [isValid]: typeof isValidImpl;
    }
}

ImportedStartInfo.prototype[isValid] = isValidImpl;