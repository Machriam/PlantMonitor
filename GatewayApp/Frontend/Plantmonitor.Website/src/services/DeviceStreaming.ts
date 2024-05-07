import { dev } from "$app/environment";
import * as signalR from "@microsoft/signalr";
import * as signalRProtocols from "@microsoft/signalr-protocol-msgpack";
import { Constants } from "~/Constants";
import { StreamingMetaData } from "./GatewayAppApi";

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
            start: async (callback: (step: number, image: string, date: Date) => Promise<void>) => {
                await connection.start();
                connection.stream("StreamPictures", new StreamingMetaData({
                    distanceInM: focusInMeter,
                    positionsToStream: [], quality: 100, resolutionDivider: sizeDivider, storeData: storeData
                }).toJSON(), device).subscribe({
                    next: async (x) => {
                        const payload = x as Uint8Array;
                        const blob = new Blob([payload.subarray(12)], { type: "image/jpeg" });
                        const date = payload.subarray(4, 12).toInt64().fromTicksToDate();
                        const imageUrl = await blob.asBase64Url();
                        await callback(payload.subarray(0, 4).toInt32(), imageUrl, date);
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
