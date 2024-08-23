export class Enum {
    static getAllKeys<T>(enumType: T): Array<keyof T> {
        //@ts-expect-error casting is possible
        return Object.keys(enumType).filter(key => isNaN(Number(key)));
    }
    static getAllValues<T>(enumType: T): Array<T[keyof T]> {
        return this.getAllKeys(enumType).map(key => enumType[key]);
    }
    static getAllEntries<T>(enumType: T): Array<{ key: keyof T, value: T[keyof T] }> {
        return this.getAllKeys(enumType).map(key => ({ key: key, value: enumType[key] }));
    }
}
