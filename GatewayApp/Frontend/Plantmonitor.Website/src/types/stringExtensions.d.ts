/// <reference types="svelte" />
declare global {
	interface String {
		isEmpty(): boolean;
		asBase64(): string;
		fromBase64(): string;
	}
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
export { };