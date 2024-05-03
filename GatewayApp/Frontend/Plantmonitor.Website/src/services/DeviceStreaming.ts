import { dev } from "$app/environment";
import * as signalR from "@microsoft/signalr";
import * as signalRProtocols from "@microsoft/signalr-protocol-msgpack";
import { Constants } from "~/Constants";

export interface IReplayedPicture {
    PictureDate: Date;
    Picture: Uint8Array;
    Steps: number;
}
export class DeviceStreaming {
    buildVideoConnection(device: string, sizeDivider = 4, focusInMeter = 10, storeData = false) {
        const url = dev ? Constants.developmentUrl : `https://${location.hostname}`;
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${url}/hub/video`, { withCredentials: false })
            .withHubProtocol(new signalRProtocols.MessagePackHubProtocol())
            .build();
        return {
            connection: connection,
            start: async (callback: (step: number, image: string) => Promise<void>) => {
                await connection.start();
                connection.stream("StreamPictures", sizeDivider, 100, focusInMeter, device, storeData).subscribe({
                    next: async (x) => {
                        const payload = x as Uint8Array;
                        const blob = new Blob([payload.subarray(4)], { type: "image/jpeg" });
                        const imageUrl = await blob.asBase64Url();
                        await callback(payload.subarray(0, 4).toInt32(), imageUrl);
                    },
                    complete: () => console.log("complete"),
                    error: (x) => console.log(x)
                });
            }
        };
    };
    replayPictures(device: string, sequenceId: string) {
        const url = dev ? Constants.developmentUrl : `https://${location.hostname}`;
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${url}/hub/video`, { withCredentials: false })
            .withHubProtocol(new signalRProtocols.MessagePackHubProtocol())
            .build();
        return {
            connection: connection,
            start: async (callback: (step: number, date: Date, image: string) => Promise<void>) => {
                await connection.start();
                connection.stream("StreamPictureSeries", device, sequenceId).subscribe({
                    next: async (x) => {
                        const payload = x as IReplayedPicture;
                        const blob = new Blob([payload.Picture], { type: "image/jpeg" });
                        const imageUrl = await blob.asBase64Url();
                        await callback(payload.Steps, payload.PictureDate, imageUrl);
                    },
                    complete: () => console.log("complete"),
                    error: (x) => console.log(x)
                });
            }
        };
    };
}
