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
    async displayVideo(url: string, image: HTMLImageElement) {
        const response = await fetch(url);
        if (!response.ok) throw Error(response.status + ' ' + response.statusText)
        if (!response.body) throw Error('ReadableStream not yet supported in this browser.')
        const reader = response.body.getReader();
        let imageBuffer: Uint8Array = new Uint8Array(1024 * 1024 * 1024);
        let bytesRead = 0;
        const headerBytes = [255, 216, 255, 224, 0, 16, 74, 70, 73, 70, 0];
        const { done, value } = await reader.read();
        if (done) return;
        for (let index = 0; index < value.length; index++) {
            let headerFound = false;
            for (let hi = 0; hi < headerBytes.length; hi++) {
                if (value[index + hi] != headerBytes[hi]) break;
                if (hi == headerBytes.length - 1) headerFound = true;
            }
            if (headerFound) {
                if (bytesRead < 100) continue;
                const frame = URL.createObjectURL(new Blob([imageBuffer.subarray(0, bytesRead)], { type: "image/jpeg" }));
                bytesRead = 0;
                image.src = frame;
                await Task.delay(1);
                URL.revokeObjectURL(frame)
                imageBuffer = new Uint8Array(1024 * 1024 * 1024);
                imageBuffer[bytesRead++] = value[index];
            }
            else imageBuffer[bytesRead++] = value[index];
        }
    }
}

