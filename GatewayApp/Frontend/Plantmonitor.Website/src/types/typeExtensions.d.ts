
interface String {
    isEmpty(): boolean;
    asBase64(): string;
    fromBase64(): string;
    urlEncoded(): string;
    base64ToByteArray(): Uint8Array;
}
interface Array<T> {
    mean(selector: (x: T) => number): number;
    toDictionary(selector: (x: T) => K): Map<K, T>;
    groupBy(this: Array<T>, selector: (x: T) => K): Map<K, T[]>;
}
interface BigInt {
    fromTicksToDate(): Date;
}
interface Number {
    roundTo(decimalPlaces: number): number;
    kelvinToCelsius(): number;
    isSuccessStatusCode(): boolean;
}
interface Blob {
    asBase64Url(): Promise<string>;
}
interface Uint8Array {
    toInt32(): number;
    toInt64(): bigint;
}

Uint8Array.prototype.toInt32 = function (this: Uint8Array): number {
    return new DataView(this.slice(0, 4).buffer).getInt32(0, true);
}

Uint8Array.prototype.toInt64 = function (this: Uint8Array): bigint {
    return new DataView(this.slice(0, 8).buffer).getBigInt64(0, true);
}

Array.prototype.mean = function (this: Array<T>, selector: (x: T) => number) {
    if (this.length == 0) return 0;
    return this.reduce((a, x) => a += selector(x), 0) / this.length;
}
Array.prototype.toDictionary = function (this: Array<T>, selector: (x: T) => K): Map<K, T> {
    if (this.length == 0) return new Map();
    return new Map(this.map(x => [selector(x), x]));
}

Array.prototype.groupBy = function (this: Array<T>, selector: (x: T) => K): Map<K, T[]> {
    if (this.length == 0) return new Map();
    const grouping = Object.groupBy(this, x => selector(x));
    const result = new Map<K, T[]>();
    for (let i = 0; i < this.length; i++) {
        const key = selector(this[i])
        if (result.has(key)) continue;
        result.set(key, grouping[key]);
    }
    return result;
}


Number.prototype.kelvinToCelsius = function (this: number): number {
    return (this - 27315) / 100;
}

Number.prototype.roundTo = function (this: number, decimalPlaces: number): number {
    return +this.toFixed(decimalPlaces);
}
BigInt.prototype.fromTicksToDate = function (this: bigint): Date {
    const zeroTime = BigInt(new Date("0001-01-01T00:00:00Z").getTime());
    const tickDivider = BigInt(10000);
    const milliseconds = Number(this / tickDivider + zeroTime);
    return new Date(milliseconds);
}
Number.prototype.isSuccessStatusCode = function (this: number): boolean {
    return this == 200 || this == 204;
}
String.prototype.isEmpty = function (this: string): boolean {
    return this === undefined || this === null || this.length == 0;
}
String.prototype.asBase64 = function (this: string): string {
    return btoa(this);
}
String.prototype.fromBase64 = function (this: string): string {
    return atob(this);
}
String.prototype.base64ToByteArray = function (this: string): Uint8Array {
    return Uint8Array.from(atob(this), c => c.charCodeAt(0));
}
String.prototype.urlEncoded = function (this: string): string {
    return encodeURIComponent(this);
}
Blob.prototype.asBase64Url = async function (this: Blob): Promise<string> {
    return new Promise((resolve) => {
        const reader = new FileReader();
        reader.onloadend = () => resolve(reader.result);
        reader.readAsDataURL(this);
    });
}

interface Performance extends Performance {
    memory?: {
        /** The maximum size of the heap, in bytes, that is available to the context. */
        jsHeapSizeLimit: number;
        /** The total allocated heap size, in bytes. */
        totalJSHeapSize: number;
        /** The currently active segment of JS heap, in bytes. */
        usedJSHeapSize: number;
    };
}