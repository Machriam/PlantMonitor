
interface String {
	isEmpty(): boolean;
	asBase64(): string;
	fromBase64(): string;
	urlEncoded(): string;
	base64ToByteArray(): Uint8Array;
}
interface Array<T> {
	mean(selector: (x: T) => number): number;
}
interface Number {
	roundTo(decimalPlaces: number): number;
	isSuccessStatusCode(): boolean;
}
interface Blob {
	asBase64Url(): Promise<string>;
}
interface Uint8Array {
	toInt32(): number;
}

Uint8Array.prototype.toInt32 = function (this: Uint8Array): number {
	return new DataView(this.slice(0, 4).buffer).getInt32(0, true);
}

Array.prototype.mean = function (this: Array<T>, selector: (x: T) => number) {
	if (this.length == 0) return 0;
	return this.reduce((a, x) => a += selector(x), 0) / this.length;
}

Number.prototype.roundTo = function (this: number, decimalPlaces: number): number {
	return +this.toFixed(decimalPlaces);
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