import { HealthState } from "~/services/GatewayAppApi";
import { formatHealthState } from "~/services/healthStateExtensions";

describe("HealthState formatting should", () => {
    test("work for NA", async () => {
        const result = formatHealthState(HealthState.NA);
        expect(result).toContain("NA");
    });
    test("work for combined", async () => {
        const result = formatHealthState(HealthState.NA | HealthState.CanSwitchOutlets | HealthState.ThermalCameraFound);
        expect(result).toContain("CanSwitchOutlets");
        expect(result).toContain("ThermalCameraFound");
        expect(result.length).toBe(2);
    });
});