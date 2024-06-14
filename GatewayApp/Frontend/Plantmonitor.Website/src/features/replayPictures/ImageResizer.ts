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