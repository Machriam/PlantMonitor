class Test {
	String: string;
	Number: number;
	constructor(text: string, number: number) {
		this.String = text;
		this.Number = number;
	}
}

describe("Uint8Array to Int32 should work", () => {
	test("with negative integers", () => {
		const data = new Uint8Array([117, 91, 255, 255]);
		expect(data.toInt32()).equal(-42123);
	});
	test("with 0", () => {
		const data = new Uint8Array([0, 0, 0, 0]);
		expect(data.toInt32()).equal(0);
	});
	test("with -1", () => {
		const data = new Uint8Array([255, 255, 255, 255]);
		expect(data.toInt32()).equal(-1);
	});
	test("with longer arrays", () => {
		const data = new Uint8Array([139, 164, 0, 0, 1, 2, 3, 4]);
		expect(data.toInt32()).equal(42123);
	});
	test("with positive integers", () => {
		const data = new Uint8Array([139, 164, 0, 0]);
		expect(data.toInt32()).equal(42123);
	});
});

describe("Mean should work", () => {
	test("with Objects", () => {
		const result = [new Test("test", 2), new Test("test2", 5), new Test("test3", 5)].mean(x => x.Number);
		expect(result).equal(4);
	});
	test("with empty Array", () => {
		const result = [].mean(x => x);
		expect(result).equal(0);
	});
});