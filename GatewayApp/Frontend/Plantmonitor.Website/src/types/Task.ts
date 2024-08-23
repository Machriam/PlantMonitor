
export class Task {
    static delay = function (ms: number) {
        return new Promise<void>(resolve => setTimeout(resolve, ms));
    }

    static createDebouncer = function (func: () => void, wait: number) {
        const debounce = function (func: () => void, wait: number, countObject: { counter: number }) {
            return () => {
                countObject.counter++;
                Task.delay(wait).then(() => {
                    countObject.counter--;
                    if (countObject.counter == 0) func();
                });
            }
        }
        return debounce(func, wait, { counter: 0 });
    }
}