
export class Task {
	static delay = function (ms: number) {
		return new Promise<void>(resolve => setTimeout(resolve, ms));
	}
}

