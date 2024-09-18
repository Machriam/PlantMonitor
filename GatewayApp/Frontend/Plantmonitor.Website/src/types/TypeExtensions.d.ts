interface Array<T> {
    mean(selector: (x: T) => number): number;
    toDictionary(selector: (x: T) => K): Map<K, T>;
    groupBy(this: Array<T>, selector: (x: T) => K): Map<K, T[]>;
}
interface Promise<T> {
    try(): Promise<{ result: T, error: unknown, hasError: boolean }>;
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

Promise.prototype.try = async function (this: Promise<T>): Promise<{ result: T, error: unknown, hasError: boolean }> {
    try {
        const result = await this;
        return { result, error: {}, hasError: false };
    }
    catch (ex) {
        return { result: {}, error: ex, hasError: true };
    }
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