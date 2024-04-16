
interface String {
	isEmpty(): boolean;
	asBase64(): string;
	fromBase64(): string;
	urlEncoded(): string;
	base64ToByteArray(): Uint8Array;
}
interface Number {
	isSuccessStatusCode(): boolean;
}
interface Blob {
	asBase64Url(): Promise<string>;
}
Number.prototype.isSuccessStatusCode = function (this: number) {
	return this == 200 || this == 204;
}
String.prototype.isEmpty = function (this: string) {
	return this === undefined || this === null || this.length == 0;
}
String.prototype.asBase64 = function (this: string) {
	return btoa(this);
}
String.prototype.fromBase64 = function (this: string) {
	return atob(this);
}
String.prototype.base64ToByteArray = function (this: string) {
	return Uint8Array.from(atob(this), c => c.charCodeAt(0));
}
String.prototype.urlEncoded = function (this: string) {
	return encodeURIComponent(this);
}
Blob.prototype.asBase64Url = async function (this: Blob) {
	return new Promise((resolve) => {
		const reader = new FileReader();
		reader.onloadend = () => resolve(reader.result);
		reader.readAsDataURL(this);
	});
}