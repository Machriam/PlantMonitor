import { dev } from "$app/environment";
import * as signalR from "@microsoft/signalr";
import * as signalRProtocols from "@microsoft/signalr-protocol-msgpack";
import { Constants } from "~/Constants";
import { CameraType, StreamingMetaData, TemperatureStreamData } from "./GatewayAppApi";
import type { IRetryPolicy } from "@microsoft/signalr";

export interface IReplayedPicture {
    Timestamp: Date;
    Steps: number;
    TemperatureInK: number;
    PictureData: Uint8Array;
}
export interface ICompressionStatus {
    Type: string;
    ZippedImageCount: number;
    TotalImages: number;
    TemperatureInK: number;
}
export interface IStoredDataStream {
    ZipFileName: string;
    CurrentStep: number;
    CompressionStatus: ICompressionStatus[];
    DownloadStatus: number;
}
export class DeviceStreamingData {
    sizeDivider = 4;
    focusInMeter = 10;
    storeData = false;
    positionsToStream: number[] = [];
}
export class RetryPolicy implements IRetryPolicy {
    nextRetryDelayInMilliseconds(): number | null {
        return 200;
    }

}
export class DeviceStreaming {
    buildCustomTourAsZipConnection(ip: string, type: CameraType, data = new DeviceStreamingData()) {
        const url = dev ? Constants.developmentUrl : `https://${location.hostname}`;
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${url}/hub/video`, { withCredentials: false })
            .withAutomaticReconnect(new RetryPolicy())
            .withHubProtocol(new signalRProtocols.MessagePackHubProtocol())
            .build();
        return {
            connection: connection,
            start: async (callback: (step: number, cameraType: string, totalImages: number,
                zippedImages: number, temperatureInK: number, downloadStatus: number, zipFile: string) => Promise<void>) => {
                await connection.start();
                connection.stream("CustomStreamAsZip", new StreamingMetaData({
                    distanceInM: data.focusInMeter,
                    positionsToStream: data.positionsToStream, quality: 100, resolutionDivider: data.sizeDivider, storeData: data.storeData, type: CameraType[type]
                }).toJSON(), ip).subscribe({
                    next: async (x) => {
                        const payload = x as IStoredDataStream;
                        for (let i = 0; i < payload.CompressionStatus.length; i++) {
                            const status = payload.CompressionStatus[i];
                            await callback(payload.CurrentStep, status.Type, status.TotalImages, status.ZippedImageCount,
                                status.TemperatureInK, payload.DownloadStatus, payload.ZipFileName);
                        }
                    },
                    complete: () => console.log("complete"),
                    error: (x) => console.log(x)
                });
            }
        };
    };
    buildVideoConnection(ip: string, type: CameraType, data = new DeviceStreamingData()) {
        const url = dev ? Constants.developmentUrl : `https://${location.hostname}`;
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${url}/hub/video`, { withCredentials: false })
            .withAutomaticReconnect(new RetryPolicy())
            .withHubProtocol(new signalRProtocols.MessagePackHubProtocol())
            .build();
        return {
            connection: connection,
            start: async (callback: (step: number, image: Blob, date: Date, temperatureInK: number) => Promise<void>) => {
                await connection.start();
                connection.stream("StreamPictures", new StreamingMetaData({
                    distanceInM: data.focusInMeter,
                    positionsToStream: data.positionsToStream, quality: 100, resolutionDivider: data.sizeDivider, storeData: data.storeData, type: CameraType[type]
                }).toJSON(), ip).subscribe({
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
    temperatureConnection(ip: string, deviceIds: string[]) {
        const url = dev ? Constants.developmentUrl : `https://${location.hostname}`;
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${url}/hub/temperatures`, { withCredentials: false })
            .withAutomaticReconnect(new RetryPolicy())
            .withHubProtocol(new signalRProtocols.MessagePackHubProtocol())
            .build();
        return {
            connection: connection,
            start: async (callback: (temperatureInC: number, device: string, time: Date) => Promise<void>) => {
                await connection.start();
                connection.stream("StreamTemperature", deviceIds, ip).subscribe({
                    next: async (x) => {
                        const payload = TemperatureStreamData.fromJS(x)
                        await callback(payload.temperatureInC, payload.device, payload.time);
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
            .withAutomaticReconnect(new RetryPolicy())
            .withHubProtocol(new signalRProtocols.MessagePackHubProtocol())
            .build();
        return {
            connection: connection,
            start: async (callback: (step: number, date: Date, image: Blob, temperature: number) => Promise<void>) => {
                await connection.start();
                connection.stream("StreamPictureSeries", device, sequenceId).subscribe({
                    next: async (x) => {
                        const payload = x as IReplayedPicture;
                        const blob = new Blob([payload.PictureData], { type: "image/jpeg" });
                        await callback(payload.Steps, payload.Timestamp, blob, payload.TemperatureInK.kelvinToCelsius());
                    },
                    complete: () => console.log("complete"),
                    error: (x) => console.log(x)
                });
            }
        };
    };
}
