import { getNearestElements } from "~/features/dashboard/GetNearestElements";

describe("get nearest elements", () => {
    test("for same size", async () => {
        const objectList = [{ name: "1", date: new Date(1001) }, { name: "2", date: new Date(1995) }, { name: "3", date: new Date(3001) }];
        const result = getNearestElements([new Date(1000), new Date(2000), new Date(3000)], objectList, x => x.date);
        expect(result).toEqual(objectList);
    });
    test("for longer size", async () => {
        const objectList = [{ name: "1", date: new Date(1001) }, { name: "2", date: new Date(1995) }, { name: "3", date: new Date(3001) }];
        const result = getNearestElements([new Date(1000), new Date(2000), new Date(3000), new Date(4000), new Date(5000)], objectList, x => x.date);
        expect(result).toEqual(objectList);
    });
    test("for shorter size", async () => {
        const objectList = [{ name: "1", date: new Date(1001) }, { name: "2", date: new Date(1995) }, { name: "3", date: new Date(3001) }];
        const result = getNearestElements([new Date(1000), new Date(3000)], objectList, x => x.date);
        const expected = [{ name: "1", date: new Date(1001) }, { name: "3", date: new Date(3001) }];
        expect(result).toEqual(expected);
    });
});