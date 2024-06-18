export class ReplayedImage {
    temperature: number | undefined;
    date: Date;
    stepCount: number;
    imageUrl: string;
    thumbnailUrl: string;
    pixelConverter: ((x: number, y: number) => number) | undefined;
}
