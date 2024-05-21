import { dev } from "$app/environment";
import * as signalR from "@microsoft/signalr";
import * as signalRProtocols from "@microsoft/signalr-protocol-msgpack";
import { Constants } from "~/Constants";
import { CameraType, StreamingMetaData } from "./GatewayAppApi";

export interface IReplayedPicture {
    Timestamp: Date;
    Steps: number;
    TemperatureInK: number;
    PictureData: Uint8Array;
}
export class DeviceStreamingData {
    sizeDivider = 4;
    focusInMeter = 10;
    storeData = false;
    positionsToStream: number[] = [];
}
export class DeviceStreaming {
    buildVideoConnection(device: string, type: CameraType, data = new DeviceStreamingData()) {
        const url = dev ? Constants.developmentUrl : `https://${location.hostname}`;
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${url}/hub/video`, { withCredentials: false })
            .withHubProtocol(new signalRProtocols.MessagePackHubProtocol())
            .build();
        return {
            connection: connection,
            start: async (callback: (step: number, image: Blob, date: Date, temperatureInK: number) => Promise<void>) => {
                await connection.start();
                connection.stream("StreamPictures", new StreamingMetaData({
                    distanceInM: data.focusInMeter,
                    positionsToStream: data.positionsToStream, quality: 100, resolutionDivider: data.sizeDivider, storeData: data.storeData, type: CameraType[type]
                }).toJSON(), device).subscribe({
                    next: async (x) => {
                        const payload = x as IReplayedPicture;
                        const blob = new Blob([payload.PictureData], { type: "image/jpeg" });
                        await callback(payload.Steps, blob, payload.Timestamp, payload.TemperatureInK);
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
            start: async (callback: (step: number, date: Date, image: string, temperature: number) => Promise<void>) => {
                await connection.start();
                connection.stream("StreamPictureSeries", device, sequenceId).subscribe({
                    next: async (x) => {
                        const payload = x as IReplayedPicture;
                        const blob = new Blob([payload.PictureData], { type: "image/jpeg" });
                        const imageUrl = await blob.asBase64Url();
                        await callback(payload.Steps, payload.Timestamp, imageUrl, payload.TemperatureInK);
                    },
                    complete: () => console.log("complete"),
                    error: (x) => console.log(x)
                });
            }
        };
    };
}
