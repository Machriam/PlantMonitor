import { MovementPoint } from "~/services/GatewayAppApi";


describe("calculateMoveTo should work", () => {
	test("with some points", async () => {
		const extension = await import("~/services/movementPointExtensions");
		//@ts-expect-error bla
		const steps = [new MovementPoint({ stepOffset: 100 }), new MovementPoint({ stepOffset: 200 }), new MovementPoint({ stepOffset: -1000 }), new MovementPoint({ stepOffset: 500 })]
		const result = extension.calculateMoveToImpl.call(steps[2], steps, 1000)
		expect(result).equal(-1700);
	})
});