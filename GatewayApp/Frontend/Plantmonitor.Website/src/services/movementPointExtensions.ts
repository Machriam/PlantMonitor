import { MovementPoint as ImportedMovementPoint } from "./GatewayAppApi";

export const calculateMoveTo = Symbol("calculateMoveTo");
export const stepsToReach = Symbol("stepsToReach");

export function calculateMoveToImpl(this: ImportedMovementPoint, points: Array<ImportedMovementPoint>, currentPosition: number) {
    const index = points.indexOf(this);
    let moveOffset = -currentPosition;
    for (let i = 0; i <= index; i++) {
        moveOffset += points[i].stepOffset;
    }
    return moveOffset;
}
export function stepsToReachImpl(this: ImportedMovementPoint, points: Array<ImportedMovementPoint>) {
    const index = points.indexOf(this);
    let result = 0;
    for (let i = 0; i <= index; i++) {
        result += points[i].stepOffset;
    }
    return result;
}

declare module "./GatewayAppApi" {
    interface MovementPoint {
        [calculateMoveTo]: typeof calculateMoveToImpl;
        [stepsToReach]: typeof stepsToReachImpl;
    }
}

ImportedMovementPoint.prototype[calculateMoveTo] = calculateMoveToImpl;
ImportedMovementPoint.prototype[stepsToReach] = stepsToReachImpl;