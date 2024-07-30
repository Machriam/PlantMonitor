export class ImageToCut {
    temperature: number | undefined;
    date: Date;
    stepCount: number;
    imageUrl: string;
    thumbnailUrl: string;
    irDataUrl: string;
    pixelConverter: ((x: number, y: number) => number) | undefined;
}