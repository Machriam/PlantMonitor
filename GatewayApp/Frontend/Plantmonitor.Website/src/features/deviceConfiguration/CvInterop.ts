
export class CvInterop {
    thermalDataToImage(source: Uint8Array, canvasId: string) {
        const canvas = document.getElementById(canvasId) as HTMLElement;
        const mat = cv.matFromArray(120, 160, cv.CV_8UC1, source,);
        cv.imshow(canvas, mat);
        mat.delete();
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
    displayVideoBuilder(image: HTMLImageElement) {
        const videoUpdater = async function (value: string) {
            if (value.length < 100) return;
            image.src = value;
        }
        return videoUpdater;
    }
}

