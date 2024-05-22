import { printError } from "./CvUtils";
export class ThermalImage {
    dataUrl?: string;
    pixelConverter?: (x: number, y: number) => number;
}

export class CvInterop {
    thermalDataToImage(source: Uint32Array): ThermalImage {
        try {
            const canvas = document.createElement("canvas");
            const mat = cv.matFromArray(120, 160, cv.CV_32SC1, source);
            const resizeMat = new cv.Mat(480, 640, cv.CV_32SC1);
            cv.normalize(mat, mat, 0, 65535, cv.NORM_MINMAX);
            mat.convertTo(mat, cv.CV_8UC1, 1 / 255);
            cv.equalizeHist(mat, mat);
            const planes = new cv.MatVector();
            const mergedHSV = new cv.MatVector();
            cv.split(mat, planes);
            const H = planes.get(0);
            const S = new cv.Mat(120, 160, cv.CV_8U, new cv.Scalar(255));
            const V = new cv.Mat(120, 160, cv.CV_8U, new cv.Scalar(255));
            cv.normalize(H, H, 100, 200, cv.NORM_MINMAX);
            mergedHSV.push_back(H);
            mergedHSV.push_back(S);
            mergedHSV.push_back(V);
            cv.merge(mergedHSV, mat);
            cv.cvtColor(mat, mat, cv.COLOR_HSV2RGB, 0);
            cv.resize(mat, resizeMat, resizeMat.size(), 0, 0);
            cv.imshow(canvas, resizeMat);
            mat.delete();
            resizeMat.delete();
            mergedHSV.delete();
            H.delete(); S.delete(); V.delete();
            planes.delete();
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

