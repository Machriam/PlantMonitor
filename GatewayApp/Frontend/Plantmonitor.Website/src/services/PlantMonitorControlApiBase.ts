export class PlantMonitorControlApiBase {
    timeout = 5000;
    controller = new AbortController();
    timeoutId = 0;
    getBaseUrl(_: string, defaultUrl: string | undefined): string {
        return defaultUrl ?? "https://localhost:443";
    }
    withTimeout<T extends PlantMonitorControlApiBase>(this: T, timeout: number): T {
        this.timeout = timeout;
        return this;
    }
    transformOptions(options: RequestInit): Promise<RequestInit> {
        this.timeoutId = setTimeout(() => this.controller.abort(), this.timeout);
        options.signal = this.controller.signal;
        return Promise.resolve(options);
    }
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    transformResult(url_: string, _response: Response, arg2: (_response: Response) => any) {
        clearTimeout(this.timeoutId);
        return arg2(_response);
    }
}