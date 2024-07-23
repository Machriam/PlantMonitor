export function resizeBase64Img(base64: string, width: number, height: number): Promise<string> {
    return new Promise((resolve, reject) => {
        try {
            const canvas = document.createElement("canvas");
            canvas.width = width;
            canvas.height = height;
            const context = canvas.getContext("2d");
            const image = new Image(width, height);
            image.onload = () => {
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

export function drawImageOnCanvas(base64: string, canvas: HTMLCanvasElement): Promise<void> {
    return new Promise((resolve, reject) => {
        try {
            const context = canvas.getContext("2d");
            if (context == null) {
                reject("Canvas context is null");
                return;
            }
            context.imageSmoothingEnabled = false;

            const image = new Image();
            image.onload = () => {
                const ratio = canvas.offsetWidth / image.width;
                const canvasWidth = image.width * ratio;
                const canvasHeight = image.height * ratio;
                canvas.width = canvasWidth
                canvas.height = canvasHeight
                canvas.style.width = `${canvasWidth}px`;
                canvas.style.height = `${canvasHeight}px`;
                context.drawImage(image, 0, 0, image.width * ratio, image.height * ratio);
                resolve();
            }
            image.src = base64;
        }
        catch (e) {
            reject(e);
        }
    });
}