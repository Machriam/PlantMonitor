import { MovementPoint as ImportedMovementPoint } from "./GatewayAppApi";

export const calculateMoveTo = Symbol("calculateMoveTo");

export function calculateMoveToImpl(this: ImportedMovementPoint, points: Array<ImportedMovementPoint>, currentPosition: number) {
    const index = points.indexOf(this);
    let moveOffset = -currentPosition;
    for (let i = 0; i <= index; i++) {
        moveOffset += points[i].stepOffset;
    }
    return moveOffset;
}

declare module "./GatewayAppApi" {
    interface MovementPoint {
        [calculateMoveTo]: typeof calculateMoveToImpl;
    }
}

ImportedMovementPoint.prototype[calculateMoveTo] = calculateMoveToImpl;