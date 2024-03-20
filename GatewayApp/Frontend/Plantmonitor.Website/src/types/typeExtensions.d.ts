interface String {
	isEmpty(): boolean;
	asBase64(): string;
	fromBase64(): string;
	urlEncoded(): string;
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
String.prototype.urlEncoded = function (this: string) {
	return encodeURIComponent(this);
}