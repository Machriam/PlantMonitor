import { ApiException } from "./GatewayAppApi";
import { dev } from "$app/environment";
import { Constants } from "../Constants";
import { pipe } from "~/types/Pipe";
export class GatewayAppApiBase {
    _noPrompt = false;
    _id = "";
    tryRegisterRunning(id: string) {
        if (this._id == id) return false;
        this._id = id;
        return true;
    }
    getBaseUrl(_: string, defaultUrl: string | undefined): string {
        const url = dev ? Constants.developmentUrl : `https://${location.hostname}`;
        return pipe(defaultUrl ?? "").isEmpty() ? url : defaultUrl!;
    }
    transformOptions(options: RequestInit): Promise<RequestInit> {
        return Promise.resolve(options);
    }
    disablePrompts() {
        this._noPrompt = true;
        return this;
    }
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    transformResult(url_: string, _response: Response, defaultFunction: (_response: Response) => Promise<any>) {
        if (pipe(_response.status).isSuccessStatusCode()) return defaultFunction(_response);
        return (async () => {
            const text = await _response.text()
            const formattedText = `${new Date().toISOString()}\n${text.replaceAll("\\n", "\n").replaceAll("\"", "")}`;
            if (!this._noPrompt) alert(formattedText);
            throw new ApiException("An error occured", _response.status, formattedText, _response.headers, null);
        })();
    }
}
