export class ClientBase {
    getBaseUrl(_: string, defaultUrl: string | undefined): string {
        return "https://localhost:7005";
    }
    transformOptions(options: RequestInit): Promise<RequestInit> {
        return Promise.resolve(options);
    }
    transformResult(url_: string, _response: Response, arg2: (_response: Response) => any) {
        return arg2(_response);
    }
}