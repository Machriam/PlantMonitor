export class Pipe {
    forDate<T extends Date>(x: T) {
        return {
            formatDate: () => this.forString(x.toISOString())
        };
    }
    forString<T extends string>(x: T) {
        return {
            toString: () => x.toString(),
            isEmpty: () => this.forBoolean(x === undefined || x === null || x.length == 0),
            getFileName: () => this.forString(x.split(/(\\|\/)/g).pop() ?? ""),
            asBase64: () => this.forString(btoa(x)),
            fromBase64: () => this.forString(atob(x)),
            base64ToByteArray: () => this.forUint8Array(Uint8Array.from(atob(x), c => c.charCodeAt(0))),
            urlEncoded: () => this.forString(encodeURIComponent(x))
        }
    }
    forUint8Array<T extends Uint8Array>(x: T) {
        return {
            toString: () => this.forString(new TextDecoder().decode(x)),

        }
    }
    forBoolean<T extends boolean>(x: T) {
        return {
            toString: () => this.forString(x.toString()),
        }
    }
}