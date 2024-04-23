class Test {
	String: string;
	Number: number;
	constructor(text: string, number: number) {
		this.String = text;
		this.Number = number;
	}
}

describe("Mean should work", () => {
	test("with Objects", () => {
		const result = [new Test("test", 2), new Test("test2", 5), new Test("test3", 5)].mean(x => x.Number);
		expect(result).equal(4);
	})
	test("with empty Array", () => {
		const result = [].mean(x => x);
		expect(result).equal(0);
	})
});