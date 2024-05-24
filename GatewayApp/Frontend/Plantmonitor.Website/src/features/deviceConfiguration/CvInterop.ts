import { printError } from "./CvUtils";
import { ColormapTypes, optionalCvFunctions } from "~/types/mirada";
export class ThermalImage {
    dataUrl?: string;
    pixelConverter?: (x: number, y: number) => number;
}


export class CvInterop {
    thermalDataToImage(source: Uint32Array): ThermalImage {
        try {
            const optCv = new optionalCvFunctions();
            const temp15Celsius = 28815;
            const canvas = document.createElement("canvas");
            const mat = cv.matFromArray(120, 160, cv.CV_32FC1, source);
            const baselineMat = new cv.Mat(120, 160, cv.CV_32FC1, new cv.Scalar(temp15Celsius));
            const resizeMat = new cv.Mat(480, 640, cv.CV_8UC1);
            cv.subtract(mat, baselineMat, mat);
            const scale = new cv.Mat(120, 160, cv.CV_32F, new cv.Scalar(1 / 10));
            cv.multiply(mat, scale, mat);
            mat.convertTo(mat, cv.CV_8UC1);
            optCv.applyColorMap(mat, mat, ColormapTypes.COLORMAP_RAINBOW);
            cv.resize(mat, resizeMat, resizeMat.size(), 0, 0);
            cv.imshow(canvas, resizeMat);
            mat.delete();
            resizeMat.delete();
            return {
                dataUrl: canvas.toDataURL(), pixelConverter: (x, y) => {
                    if (x < 0) x = 0;
                    if (y < 0) y = 0;
                    if (y > 480) y = 480;
                    if (x > 640) x = 640;
                    const result = source[Math.floor(x / 4) + Math.floor(y / 4) * 160].kelvinToCelsius();
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
        const height = 480;
        const width = 640;
        const src = new cv.Mat(height, width, cv.CV_8UC4);
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
            const image = await value.asBase64Url();
            if (image.length < 100) return;
            imageElement.src = image;
        }
        return videoUpdater;
    }
}

