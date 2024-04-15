export class GatewayAppApiBase {
    getBaseUrl(_: string, defaultUrl: string | undefined): string {
        return defaultUrl?.isEmpty() ?? true ? "https://localhost:7005" : defaultUrl!;
    }
    transformOptions(options: RequestInit): Promise<RequestInit> {
        return Promise.resolve(options);
    }
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    transformResult(url_: string, _response: Response, arg2: (_response: Response) => any) {
        return arg2(_response);
    }
}