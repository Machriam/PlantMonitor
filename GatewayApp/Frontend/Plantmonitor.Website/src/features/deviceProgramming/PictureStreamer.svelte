<script lang="ts">
    import type {HubConnection} from "@microsoft/signalr";
    import {DeviceStreaming} from "~/services/DeviceStreaming";
    import {CameraType} from "~/services/GatewayAppApi";
    import {CvInterop, ThermalImage} from "../deviceConfiguration/CvInterop";
    import {dev} from "$app/environment";
    import {TooltipCreator, TooltipCreatorResult} from "../reuseableComponents/TooltipCreator";

    let canvasId = crypto.randomUUID();
    const cvInterop = new CvInterop();
    let hubConnection: HubConnection | undefined;
    let temperature: number | undefined;
    let currentTime: Date | undefined;
    let lastPointerPosition: MouseEvent | undefined;
    let tooltip: TooltipCreatorResult | undefined;
    const irImageBytes = 120 * 160 * 4;
    let pixelConverter: ((x: number, y: number) => number) | undefined;
    export let firstImageReceived = false;
    export let firstDataReceived = false;
    export let currentPosition: number | undefined = undefined;
    export const showPreview = function (ip: string, type: CameraType, focusInMeter: number) {
        const connection = new DeviceStreaming().buildVideoConnection(ip, type, {
            focusInMeter: focusInMeter / 100,
            storeData: false,
            positionsToStream: [],
            sizeDivider: type == CameraType.IR ? 1 : dev ? 8 : 4
        });
        hubConnection?.stop();
        hubConnection = connection.connection;
        connection.start(async (step, data, date, temperatureInK) => {
            const image = document.getElementById(canvasId) as HTMLImageElement;
            firstDataReceived = true;
            currentPosition = step;
            currentTime = date;
            if (type == CameraType.IR) {
                temperature = temperatureInK.kelvinToCelsius();
                const convertedImage =
                    data.size == irImageBytes ? cvInterop.thermalDataToImage(new Uint32Array(await data.arrayBuffer())) : {};
                pixelConverter = convertedImage.pixelConverter;
                image.src = convertedImage.dataUrl ?? "";
                updateTooltip();
            } else {
                pixelConverter = undefined;
                image.src = await data.asBase64Url();
            }
            if (!image.src.isEmpty()) firstImageReceived = true;
        });
    };
    export const storeDataStream = function (positionsToReach: number[], ip: string, type: CameraType, focusInMeter: number) {
        const connection = new DeviceStreaming().buildVideoConnection(ip, type, {
            focusInMeter: focusInMeter / 100,
            storeData: true,
            positionsToStream: positionsToReach,
            sizeDivider: 1
        });
        hubConnection?.stop();
        hubConnection = connection.connection;
        connection.start(async (step, data, date, temperatureInK) => {
            if (hubConnection == undefined) return;
            const image = document.getElementById(canvasId) as HTMLImageElement;
            firstDataReceived = true;
            currentPosition = step;
            currentTime = date;
            if (type == CameraType.IR) {
                temperature = temperatureInK.kelvinToCelsius();
                const convertedImage =
                    data.size == irImageBytes ? cvInterop.thermalDataToImage(new Uint32Array(await data.arrayBuffer())) : {};
                pixelConverter = convertedImage.pixelConverter;
                image.src = convertedImage.dataUrl ?? "";
                updateTooltip();
            } else {
                pixelConverter = undefined;
                image.src = await data.asBase64Url();
            }
            if (!image.src.isEmpty()) firstImageReceived = true;
        });
    };
    export const stopStreaming = function () {
        hubConnection?.stop();
        hubConnection = undefined;
        tooltip?.dispose();
        tooltip = undefined;
    };
    function updateTooltip() {
        if (pixelConverter == null || tooltip == undefined || lastPointerPosition == null) return;
        const value = pixelConverter(lastPointerPosition.offsetX, lastPointerPosition.offsetY);
        tooltip.updateFunction(lastPointerPosition, value.toFixed(2) + " °C");
    }
</script>

<div class="col-md-12 d-flex flex-row">
    {#if currentPosition != undefined}
        <div>Current Position: {currentPosition}</div>
    {/if}
    {#if currentPosition != undefined}
        <div class="ms-3">Image Time: {currentTime?.toLocaleTimeString()}</div>
    {/if}
    {#if temperature != undefined}
        <div class="ms-3">Temperature: {temperature}</div>
    {/if}
</div>
<div class="col-md-12"></div>
<img
    on:mouseenter={(x) => {
        if (tooltip != undefined || pixelConverter == null) return;
        lastPointerPosition = x;
        tooltip = TooltipCreator.CreateTooltip("", x);
        updateTooltip();
    }}
    on:mouseleave={() => {
        if (tooltip == undefined) return;
        tooltip.dispose();
        tooltip = undefined;
    }}
    on:pointermove={(x) => {
        lastPointerPosition = x;
        updateTooltip();
    }}
    alt="preview"
    id={canvasId} />
