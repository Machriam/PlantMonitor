class Test {
    String: string;
    Number: number;
    constructor(text: string, number: number) {
        this.String = text;
        this.Number = number;
    }
}
describe("Uint8Array to Int64 should work", () => {
    test("with negative integers", () => {
        const data = new Uint8Array([129, 65, 230, 205, 122, 107, 220, 8]);
        expect(data.toInt64()).equal(638503422364369281n);
    });
});

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
describe("ticksToDate should work", () => {
    test("default", () => {
        const ticks = BigInt(638503422364369281n);
        const expected = new Date("2024-05-03T14:10:36.436Z")
        const result = ticks.fromTicksToDate();
        expect(result.getTime()).equal(expected.getTime());
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

describe("toDictionary should work", () => {
    test("with Objects", () => {
        const result = [new Test("test", 2), new Test("test2", 5), new Test("test3", 6)].toDictionary(x => x.Number);
        expect(JSON.stringify(Object.fromEntries(result.entries()))).equal('{"2":{"String":"test","Number":2},"5":{"String":"test2","Number":5},"6":{"String":"test3","Number":6}}');
    });
    test("with Duplicate Keys", () => {
        const result = [new Test("test", 2), new Test("test2", 5), new Test("test3", 5)].toDictionary(x => x.Number);
        expect(JSON.stringify(Object.fromEntries(result.entries()))).equal('{"2":{"String":"test","Number":2},"5":{"String":"test3","Number":5}}');
    });
    test("with empty Array", () => {
        const result = [].toDictionary(x => x);
        expect(JSON.stringify(Object.fromEntries(result.entries()))).equal("{}");
    });
});

describe("groupBy should work", () => {
    test("with Objects", () => {
        const result = [new Test("test", 2), new Test("test2", 5), new Test("test3", 6)].groupBy(x => x.Number);
        expect(JSON.stringify(Object.fromEntries(result.entries()))).equal('{"2":[{"String":"test","Number":2}],"5":[{"String":"test2","Number":5}],"6":[{"String":"test3","Number":6}]}');
    });
    test("with Duplicate Keys", () => {
        const result = [new Test("test", 2), new Test("test2", 5), new Test("test3", 5)].groupBy(x => x.Number);
        expect(JSON.stringify(Object.fromEntries(result.entries()))).equal('{"2":[{"String":"test","Number":2}],"5":[{"String":"test2","Number":5},{"String":"test3","Number":5}]}');
    });
    test("with empty Array", () => {
        const result = [].groupBy(x => x);
        expect(JSON.stringify(Object.fromEntries(result.entries()))).equal("{}");
    });
});