import { pipe } from "~/types/Pipe";

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
        const data = pipe(new Uint8Array([129, 65, 230, 205, 122, 107, 220, 8]));
        expect(data.toInt64()).equal(638503422364369281n);
    });
});

describe("Uint8Array to Int32 should work", () => {
    test("with negative integers", () => {
        const data = pipe(new Uint8Array([117, 91, 255, 255]));
        expect(data.toInt32()).equal(-42123);
    });
    test("with 0", () => {
        const data = pipe(new Uint8Array([0, 0, 0, 0]));
        expect(data.toInt32()).equal(0);
    });
    test("with -1", () => {
        const data = pipe(new Uint8Array([255, 255, 255, 255]));
        expect(data.toInt32()).equal(-1);
    });
    test("with longer arrays", () => {
        const data = pipe(new Uint8Array([139, 164, 0, 0, 1, 2, 3, 4]));
        expect(data.toInt32()).equal(42123);
    });
    test("with positive integers", () => {
        const data = pipe(new Uint8Array([139, 164, 0, 0]));
        expect(data.toInt32()).equal(42123);
    });
});

describe("Mean should work", () => {
    test("with Objects", () => {
        const result = pipe([new Test("test", 2), new Test("test2", 5), new Test("test3", 5)]).mean(x => x.Number).valueOf();
        expect(result).equal(4);
    });
    test("with empty Array", () => {
        const result = pipe([]).mean(x => x).valueOf();
        expect(result).equal(0);
    });
});

describe("toDictionary should work", () => {
    test("with Objects", () => {
        const result = pipe([new Test("test", 2), new Test("test2", 5), new Test("test3", 6)]).toDictionary(x => x.Number);
        expect(JSON.stringify(Object.fromEntries(result.entries()))).equal('{"2":{"String":"test","Number":2},"5":{"String":"test2","Number":5},"6":{"String":"test3","Number":6}}');
    });
    test("with Duplicate Keys", () => {
        const result = pipe([new Test("test", 2), new Test("test2", 5), new Test("test3", 5)]).toDictionary(x => x.Number);
        expect(JSON.stringify(Object.fromEntries(result.entries()))).equal('{"2":{"String":"test","Number":2},"5":{"String":"test3","Number":5}}');
    });
    test("with empty Array", () => {
        const result = pipe([]).toDictionary(x => x);
        expect(JSON.stringify(Object.fromEntries(result.entries()))).equal("{}");
    });
});

describe("groupBy should work", () => {
    test("with Objects", () => {
        const result = pipe([new Test("test", 2), new Test("test2", 5), new Test("test3", 6)]).groupBy(x => x.Number);
        expect(JSON.stringify(Object.fromEntries(result.entries()))).equal('{"2":[{"String":"test","Number":2}],"5":[{"String":"test2","Number":5}],"6":[{"String":"test3","Number":6}]}');
    });
    test("with Duplicate Keys", () => {
        const result = pipe([new Test("test", 2), new Test("test2", 5), new Test("test3", 5)]).groupBy(x => x.Number);
        expect(JSON.stringify(Object.fromEntries(result.entries()))).equal('{"2":[{"String":"test","Number":2}],"5":[{"String":"test2","Number":5},{"String":"test3","Number":5}]}');
    });
    test("with empty Array", () => {
        const result = pipe([]).groupBy(x => x);
        expect(JSON.stringify(Object.fromEntries(result.entries()))).equal("{}");
    });
});

describe("numeric sorting should work", () => {
    test("default case", () => {
        const result = pipe(["asdf123", "asdf1", "asdf2", "bsdf999", "bsdf1", "asdf99"]).orderByNumericString(x => x).toArray();
        expect(JSON.stringify(result)).equal('["asdf1","asdf2","asdf99","asdf123","bsdf1","bsdf999"]');
    });
});