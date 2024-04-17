import { ApiException } from "./GatewayAppApi";
import { dev } from "$app/environment";

export class GatewayAppApiBase {
    readonly developmentUrl = "https://localhost:7005";
    getBaseUrl(_: string, defaultUrl: string | undefined): string {
        const url = dev ? this.developmentUrl : `https://${location.hostname}`;
        return defaultUrl?.isEmpty() ?? true ? url : defaultUrl!;
    }
    transformOptions(options: RequestInit): Promise<RequestInit> {
        return Promise.resolve(options);
    }
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    transformResult(url_: string, _response: Response, defaultFunction: (_response: Response) => Promise<any>) {
        if (_response.status.isSuccessStatusCode()) return defaultFunction(_response);
        return (async () => {
            const text = await _response.text()
            const formattedText = `${new Date().toISOString()}\n${text.replaceAll("\\n", "\n").replaceAll("\"", "")}`;
            alert(formattedText);
            throw new ApiException("An error occured", _response.status, formattedText, _response.headers, null);
        })();
    }
}
