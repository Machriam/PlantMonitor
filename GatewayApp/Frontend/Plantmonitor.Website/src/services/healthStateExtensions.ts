import { HealthState } from "./GatewayAppApi";

export function formatHealthState(state: HealthState) {
    const flagCount = (Object.getOwnPropertyNames(HealthState).length / 2) - 1;
    const result = [];
    for (let i = 1; i < 1 << flagCount; i = i << 1) {
        if (state & i) result.push(HealthState[i]);
    }
    if (result.length == 0) return [HealthState[HealthState.NA]];
    return result;
}