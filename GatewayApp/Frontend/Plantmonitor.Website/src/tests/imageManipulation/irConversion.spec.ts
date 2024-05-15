import { MovementPoint } from "~/services/GatewayAppApi";
import { promises as fs } from "fs";
import path from 'path';


describe("Test", () => {
	test("Test", async () => {
		const filePath = path.join(__dirname, 'test');
		const fileContent: Uint8Array = await fs.readFile(filePath);
		console.log(new DataView(fileContent.slice(0, 4).buffer).getInt32(0, true));
		console.log(fileContent.length/4+" should be "+(160*120));
	})
});