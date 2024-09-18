type PipePrimitives = number | string | Date | Blob | Uint8Array;
type PipeExtensions = StringExtensions | NumberExtensions | DateExtensions | BlobExtensions | Uint8Extensions;

class Apply<T extends PipePrimitives> {
    constructor(private applyArg: T) { }
    apply<U extends string>(arg: (x: T) => U): StringExtensions;
    apply<U extends number>(arg: (x: T) => U): NumberExtensions;
    apply<U extends Date>(arg: (x: T) => U): DateExtensions;
    apply<U extends Blob>(arg: (x: T) => U): BlobExtensions;
    apply<U extends Uint8Array>(arg: (x: T) => U): Uint8Extensions;
    apply<U extends PipePrimitives>(arg: (x: T) => U): PipeExtensions {
        const result = arg(this.applyArg);
        const resultType = typeof result;
        switch (resultType) {
            case 'string': return pipe(result as string);
            case 'number': return pipe(result as number);
            case 'object': {
                if (result instanceof Date) return pipe(result as Date);
                if (result instanceof Blob) return pipe(result as Blob);
                if (result instanceof Uint8Array) return pipe(result as Uint8Array);
                throw new Error("Invalid type");
            }
            default: throw new Error("Invalid type");
        }
    }
    valueOf(): T {
        return this.applyArg;
    }
}

class Uint8Extensions extends Apply<Uint8Array> {
    constructor(private x: Uint8Array) { super(x); }
    toInt32() {
        return new DataView(this.x.slice(0, 4).buffer).getInt32(0, true);
    }
    toInt64() {
        return new DataView(this.x.slice(0, 8).buffer).getBigInt64(0, true);
    }

}

class DateExtensions extends Apply<Date> {
    constructor(private x: Date) { super(x); }
    formatDate() { return pipe(this.x.toISOString()); }
    orderByDescending(b: Date): number {
        return this.x > b ? 1 : -1;
    }
    orderBy(b: Date): number {
        return this.x > b ? -1 : 1;
    }
}

class BlobExtensions extends Apply<Blob> {
    constructor(private x: Blob) { super(x); }
    asBase64Url(): Promise<StringExtensions> {
        return new Promise((resolve) => {
            const reader = new FileReader();
            reader.onloadend = () => resolve(pipe(reader.result as string));
            reader.readAsDataURL(this.x);
        });
    }
}

class StringExtensions extends Apply<string> {
    constructor(private x: string) { super(x); }
    isEmpty() { return this.x === undefined || this.x === null || this.x.length == 0 }
    getFileName() { return this.x.split(/(\\|\/)/g).pop() ?? ""; }
    asBase64() { return pipe(btoa(this.x)); }
    fromBase64() { return pipe(atob(this.x)); }
    base64ToByteArray() { return Uint8Array.from(atob(this.x), c => c.charCodeAt(0)); }
    urlEncoded() { return pipe(encodeURIComponent(this.x)); }
    toString() { return this.x; }
    valueOf(): string { return this.x; }
}
class NumberExtensions extends Apply<number> {
    constructor(private x: number) {
        super(x);
    }
    isSuccessStatusCode() {
        return this.x == 200 || this.x == 204;
    }
    kelvinToCelsius() {
        return (this.x - 27315) / 100;
    }
    roundTo(decimalPlaces: number) {
        return +this.x.toFixed(decimalPlaces);
    }
}
export function pipe(x: string): StringExtensions;
export function pipe(x: number): NumberExtensions;
export function pipe(x: Date): DateExtensions;
export function pipe(x: Blob): BlobExtensions;
export function pipe(x: Uint8Array): Uint8Extensions;
export function pipe(x: PipePrimitives): PipeExtensions {
    const type = typeof x;
    x instanceof Date
    switch (type) {
        case 'string': return new StringExtensions(x as string);
        case 'number': return new NumberExtensions(x as number);
        case 'object': {
            if (x instanceof Date) return new DateExtensions(x as Date);
            if (x instanceof Blob) return new BlobExtensions(x as Blob);
            if (x instanceof Uint8Array) return new Uint8Extensions(x as Uint8Array);
            throw new Error("Invalid type");
        }
        default: throw new Error("Invalid type");
    }
}