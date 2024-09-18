import type { Mat } from "mirada/dist/src/types/opencv/Mat";
import { printError } from "./CvUtils";
import { ColormapTypes, OptionalCvFunctions } from "~/types/mirada";
import { pipe } from "~/types/Pipe";
export const IrScalingHeight = 480;
export const IrScalingWidth = 640;
export const IrHeight = 120;
export const IrWidth = 160;
export class ThermalImage {
    dataUrl?: string;
    pixelConverter?: (x: number, y: number) => number;
}

export type ImageOffsetCalculator = {
    leftControl: (x: number) => void;
    topControl: (x: number) => void;
    visOpacity: (x: number) => void;
    delete: () => void;
};

export class CvInterop {
    calculateImageOffset(irData: string, visImageData: string): ImageOffsetCalculator {
        const irCanvas = document.createElement("canvas");
        const visCanvas = document.createElement("canvas");
        const div = document.createElement("div");
        div.style.position = "relative";
        const article = document.getElementsByClassName("content")[0];
        article.appendChild(div);
        div.appendChild(irCanvas);
        div.appendChild(visCanvas);
        irCanvas.style.position = "absolute";
        visCanvas.style.position = "absolute";
        visCanvas.style.opacity = "0.5";
        const irImage = document.createElement("img") as HTMLImageElement;
        const visImage = document.createElement("img") as HTMLImageElement;
        irImage.onload = () => {
            const irMat = cv.imread(irImage);
            visImage.src = visImageData;
            visImage.onload = () => {
                const visMat = cv.imread(visImage);
                const ratio = irMat.size().height / visMat.size().height;
                cv.resize(visMat, visMat, new cv.Size(0, 0), ratio, ratio);
                cv.imshow(irCanvas, irMat);
                cv.imshow(visCanvas, visMat);
                div.style.height = (irMat.size().height + 100) + "px";
                irMat.delete();
                visMat.delete();
            }
        }
        irImage.src = irData;
        return {
            leftControl: (x) => irCanvas.style.left = x + "px",
            topControl: (x) => irCanvas.style.top = x + "px",
            visOpacity: (x) => visCanvas.style.opacity = x + "",
            delete: () => article.removeChild(div)
        };
    }

    canny(src: Mat, dest: Mat, threshold1: number, threshold2: number, apertureSize: number = 3, l2Gradient: boolean = false) {
        cv.cvtColor(src, src, cv.COLOR_RGBA2GRAY, 0);
        cv.Canny(src, dest, threshold1, threshold2, apertureSize, l2Gradient);
    }

    kernelFilter(src: Mat, dest: Mat, kernelArray: number[], arrayWidth: number, arrayHeight: number, normalize = true) {
        if (normalize) {
            const sum = kernelArray.reduce((a, b) => a + b, 0);
            if (sum != 0) kernelArray = kernelArray.map(x => x / sum);
        }
        const kernel = cv.matFromArray(arrayHeight, arrayWidth, cv.CV_32FC1, kernelArray);
        const anchor = new cv.Point(-1, -1);
        cv.filter2D(src, dest, cv.CV_8U, kernel, anchor, 0, cv.BORDER_DEFAULT);
        kernel.delete();
    }

    medianBlur(src: Mat, dest: Mat, ksize: number) {
        cv.medianBlur(src, dest, ksize);
    }
    thermalDataToImage(source: Uint32Array): ThermalImage {
        try {
            const optCv = new OptionalCvFunctions();
            const temp15Celsius = 28815;
            const canvas = document.createElement("canvas");
            const mat = cv.matFromArray(IrHeight, IrWidth, cv.CV_32FC1, source);
            const baselineMat = new cv.Mat(IrHeight, IrWidth, cv.CV_32FC1, new cv.Scalar(temp15Celsius));
            const resizeMat = new cv.Mat(IrScalingHeight, IrScalingWidth, cv.CV_8UC1);
            cv.subtract(mat, baselineMat, mat);
            const scale = new cv.Mat(IrHeight, IrWidth, cv.CV_32F, new cv.Scalar(1 / 10));
            cv.multiply(mat, scale, mat);
            mat.convertTo(mat, cv.CV_8UC1);
            optCv.applyColorMap(mat, mat, ColormapTypes.COLORMAP_RAINBOW);
            cv.resize(mat, resizeMat, resizeMat.size(), 0, 0);
            this.kernelFilter(resizeMat, resizeMat, [1, 1, 1, 1, -8.1, 1, 1, 1, 1], 3, 3);
            this.medianBlur(resizeMat, resizeMat, 3);
            cv.imshow(canvas, resizeMat);
            mat.delete();
            baselineMat.delete();
            scale.delete();
            resizeMat.delete();
            return {
                dataUrl: canvas.toDataURL(), pixelConverter: (x, y) => {
                    if (x < 0) x = 0;
                    if (y < 0) y = 0;
                    if (y > IrScalingHeight) y = IrScalingHeight;
                    if (x > IrScalingWidth) x = IrScalingWidth;
                    if (source.length < IrWidth * IrHeight) return -9999;
                    const result = pipe(source[Math.floor(x / 4) + Math.floor(y / 4) * 160]).kelvinToCelsius();
                    return result;
                }
            };
        }
        catch (e) {
            printError(e);
        }
        return {};
    }
    extractImages(source: string, dest: HTMLCanvasElement) {
        const video = new cv.VideoCapture(source);
        const src = new cv.Mat(IrScalingHeight, IrScalingWidth, cv.CV_8UC4);
        const fps = 30;
        function processVideo() {
            const begin = Date.now();
            video.read(src);
            cv.imshow(dest, src);
            const delay = 1000 / fps - (Date.now() - begin);
            setTimeout(processVideo, delay);
        }
        setTimeout(processVideo, 0);
    }
    displayVideoBuilder(imageElement: HTMLImageElement) {
        const videoUpdater = async function (value: Blob) {
            const image = (await pipe(value).asBase64Url()).valueOf();
            if (image.length < 100) return;
            imageElement.src = image;
        }
        return videoUpdater;
    }
}