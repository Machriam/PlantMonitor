export class GatewayAppApiBase {
    getBaseUrl(_: string, defaultUrl: string | undefined): string {
        return defaultUrl ?? "https://localhost:7005";
    }
    transformOptions(options: RequestInit): Promise<RequestInit> {
        return Promise.resolve(options);
    }
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    transformResult(url_: string, _response: Response, arg2: (_response: Response) => any) {
        return arg2(_response);
    }
}