import { pipe } from "~/types/Pipe";
import { Task } from "~/types/Task";

export function resizeBase64Img(base64: string, width: number, height: number): Promise<string> {
    return new Promise((resolve, reject) => {
        try {
            const canvas = document.createElement("canvas");
            canvas.width = width;
            canvas.height = height;
            const context = canvas.getContext("2d");
            const image = new Image(width, height);
            image.onload = async () => {
                await image.decode();
                context?.drawImage(image, 0, 0, width, height);
                const result = canvas.toDataURL("image/jpeg");
                resolve(result);
            }
            image.src = base64;
        }
        catch (e) {
            reject(e);
        }
    });
}

export function cropImage(originalImage: string, polygon: { x: number, y: number }[]): Promise<string> {
    return new Promise((resolve, reject) => {
        try {
            const maskCanvas = document.createElement("canvas") as HTMLCanvasElement;
            const resultCanvas = document.createElement("canvas") as HTMLCanvasElement;
            const imgCanvas = document.createElement("canvas") as HTMLCanvasElement;
            const maskContext = maskCanvas.getContext("2d");
            const imgContext = imgCanvas.getContext("2d");
            const resultContext = resultCanvas.getContext("2d");
            if (imgContext == null || maskContext == null || resultContext == null) {
                reject("Canvas context is null");
                return;
            }
            const image = new Image();
            image.onload = () => {
                imgCanvas.width = image.width;
                imgCanvas.height = image.height;
                maskCanvas.width = image.width;
                maskCanvas.height = image.height;

                imgContext.drawImage(image, 0, 0);
                maskContext.fillStyle = "black";
                maskContext.fillRect(0, 0, maskCanvas.width, maskCanvas.height);
                maskContext.beginPath();
                const cropDimensions = { x1: Math.min(...polygon.map(p => p.x)), x2: Math.max(...polygon.map(p => p.x)), y1: Math.min(...polygon.map(p => p.y)), y2: Math.max(...polygon.map(p => p.y)) };
                const cropWidth = cropDimensions.x2 - cropDimensions.x1;
                const cropHeight = cropDimensions.y2 - cropDimensions.y1;
                resultCanvas.width = cropWidth;
                resultCanvas.height = cropHeight;
                polygon.forEach((point, index) => {
                    if (index === 0) {
                        maskContext.moveTo(point.x, point.y);
                    } else {
                        maskContext.lineTo(point.x, point.y);
                    }
                });
                maskContext.closePath();
                maskContext.fillStyle = "white";
                maskContext.fill();
                maskContext.clip();
                maskContext.globalCompositeOperation = "multiply";

                maskContext.drawImage(imgCanvas, 0, 0,);

                resultContext.drawImage(maskCanvas, cropDimensions.x1, cropDimensions.y1, cropWidth, cropHeight, 0, 0, cropWidth, cropHeight);
                resolve(resultCanvas.toDataURL("image/jpeg"));
            };
            image.src = originalImage;
        } catch (e) {
            reject(e);
        }
    });
}

export function drawImageOnCanvas(base64: string, canvas: HTMLCanvasElement): Promise<{ ratio: number }> {
    return new Promise((resolve, reject) => {
        try {
            const context = canvas.getContext("2d");
            if (context == null) {
                reject("Canvas context is null");
                return;
            }
            context.imageSmoothingEnabled = false;

            const image = new Image();
            image.onload = async () => {
                let counter = 0;
                let decodeResult = pipe(image.decode()).try();
                while ((await decodeResult).hasError && counter < 100) {
                    counter++;
                    decodeResult = pipe(image.decode()).try();
                    await Task.delay(10);
                }
                const ratio = canvas.offsetWidth / image.width;
                const canvasWidth = image.width * ratio;
                const canvasHeight = image.height * ratio;
                canvas.width = canvasWidth
                canvas.height = canvasHeight
                canvas.style.width = `${canvasWidth}px`;
                canvas.style.height = `${canvasHeight}px`;
                context.drawImage(image, 0, 0, image.width * ratio, image.height * ratio);
                resolve({ ratio: ratio });
            }
            image.src = base64;
        }
        catch (e) {
            reject(e);
        }
    });
}