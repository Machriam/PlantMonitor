import { Task } from "~/types/task";

export class CvInterop {
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
    displayVideoBuilder(image: HTMLImageElement) {
        let bytesRead = 0;
        const headerBytes = [255, 216, 255, 224, 0, 16, 74, 70, 73, 70, 0];
        let imageBuffer = new Uint8Array(1024 * 1024 * 1024);
        const videoUpdater = async function (value: Uint8Array) {
            for (let index = 0; index < value.length; index++) {
                let headerFound = false;
                for (let hi = 0; hi < headerBytes.length; hi++) {
                    if (value[index + hi] != headerBytes[hi]) break;
                    if (hi == headerBytes.length - 1) headerFound = true;
                }
                if (headerFound) {
                    if (bytesRead < 100) continue;
                    const blob = new Blob([imageBuffer.subarray(0, bytesRead)], { type: "image/jpeg" });
                    const frame = URL.createObjectURL(blob);
                    bytesRead = 0;
                    image.src = frame;
                    console.log(await blob.asBase64Url())
                    await Task.delay(1000);
                    URL.revokeObjectURL(frame)
                    imageBuffer = new Uint8Array(1024 * 1024 * 1024);
                    imageBuffer[bytesRead++] = value[index];
                }
                else imageBuffer[bytesRead++] = value[index];
            }
        }
        return videoUpdater;
    }
}

