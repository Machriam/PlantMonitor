import * as signalR from "@microsoft/signalr";
import * as signalRProtocols from "@microsoft/signalr-protocol-msgpack";
import { GatewayAppApiBase } from "./GatewayAppApiBase";

export class DeviceStreaming {
    buildVideoConnection(device: string) {
        const developmentUrl = new GatewayAppApiBase().developmentUrl;
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${developmentUrl}/hub/video`, { withCredentials: false })
            .withHubProtocol(new signalRProtocols.MessagePackHubProtocol())
            .build();
        return {
            connection: connection,
            start: async (callback: (image: string) => Promise<void>) => {
                await connection.start();
                connection.stream("StreamPictures", 2, 100, 0.1, device).subscribe({
                    next: async (x) => {
                        const payload = x as Uint8Array;
                        const blob = new Blob([payload], { type: "image/jpeg" });
                        const imageUrl = await blob.asBase64Url();
                        await callback(imageUrl);
                    },
                    complete: () => console.log("complete"),
                    error: (x) => console.log(x)
                });
            }
        };
    };
}
