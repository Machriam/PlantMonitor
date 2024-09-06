<script lang="ts">
    import type {HubConnection} from "@microsoft/signalr";
    import {DeviceStreaming} from "~/services/DeviceStreaming";
    import {CameraType, DeviceClient} from "~/services/GatewayAppApi";
    import {CvInterop, IrHeight, IrWidth, ThermalImage} from "../deviceConfiguration/CvInterop";
    import {dev} from "$app/environment";
    import {TooltipCreator, TooltipCreatorResult} from "../reuseableComponents/TooltipCreator";
    import {Download} from "~/types/Download";
    import {GatewayAppApiBase} from "~/services/GatewayAppApiBase";
    import {Enum} from "~/types/Enum";

    let _canvasId = crypto.randomUUID();
    const _cvInterop = new CvInterop();
    let _hubConnection: HubConnection | undefined;
    let _temperature: number | undefined;
    let _currentTime: Date | undefined;
    let _lastPointerPosition: MouseEvent | undefined;
    let _tooltip: TooltipCreatorResult | undefined;
    let _customPhotoStreamProgress: {irZipProgress: string; visZipProgress: string; downloadProgress: number} = {
        irZipProgress: "",
        visZipProgress: "",
        downloadProgress: 0
    };
    const _irImageBytes = IrHeight * IrWidth * 4;
    let _pixelConverter: ((x: number, y: number) => number) | undefined;
    let _customPhotoStreaming = false;
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
        _hubConnection?.stop();
        _hubConnection = connection.connection;
        _customPhotoStreaming = false;
        connection.start(async (step, data, date, temperatureInK) => {
            const image = document.getElementById(_canvasId) as HTMLImageElement;
            firstDataReceived = true;
            currentPosition = step;
            _currentTime = date;
            if (type == CameraType.IR) {
                _temperature = temperatureInK.kelvinToCelsius();
                const convertedImage =
                    data.size == _irImageBytes ? _cvInterop.thermalDataToImage(new Uint32Array(await data.arrayBuffer())) : {};
                _pixelConverter = convertedImage.pixelConverter;
                image.src = convertedImage.dataUrl ?? "";
                updateTooltip();
            } else {
                _pixelConverter = undefined;
                image.src = await data.asBase64Url();
            }
            if (!image.src.isEmpty()) firstImageReceived = true;
        });
    };
    export const customPhotoStream = function (ip: string, type: CameraType, focusInMeter: number) {
        const connection = new DeviceStreaming().buildCustomTourAsZipConnection(ip, type, {
            focusInMeter: focusInMeter / 100,
            storeData: true,
            positionsToStream: [],
            sizeDivider: 1
        });
        _hubConnection?.stop();
        _hubConnection = connection.connection;
        _customPhotoStreaming = true;
        connection.start(async (step, cameraType, totalImages, zippedImages, temperatureInK, downloadStatus, zipFile) => {
            if (_hubConnection == undefined) return;
            firstDataReceived = true;
            _customPhotoStreamProgress.downloadProgress = downloadStatus;
            currentPosition = step;
            if (downloadStatus == 1) {
                _hubConnection.stop();
                Download.downloadFromUrl(new GatewayAppApiBase().getBaseUrl("", "") + zipFile);
            }
            if (cameraType == CameraType[CameraType.IR]) {
                _customPhotoStreamProgress.irZipProgress = `${zippedImages}/${totalImages}`;
                _temperature = temperatureInK.kelvinToCelsius();
            } else {
                _customPhotoStreamProgress.visZipProgress = `${zippedImages}/${totalImages}`;
            }
            _customPhotoStreamProgress = _customPhotoStreamProgress;
        });
    };

    export const storeDataStream = function (positionsToReach: number[], ip: string, type: CameraType, focusInMeter: number) {
        const connection = new DeviceStreaming().buildVideoConnection(ip, type, {
            focusInMeter: focusInMeter / 100,
            storeData: true,
            positionsToStream: positionsToReach,
            sizeDivider: 1
        });
        _hubConnection?.stop();
        _hubConnection = connection.connection;
        _customPhotoStreaming = false;
        connection.start(async (step, data, date, temperatureInK) => {
            if (_hubConnection == undefined) return;
            const image = document.getElementById(_canvasId) as HTMLImageElement;
            firstDataReceived = true;
            currentPosition = step;
            _currentTime = date;
            if (type == CameraType.IR) {
                _temperature = temperatureInK.kelvinToCelsius();
                const convertedImage =
                    data.size == _irImageBytes ? _cvInterop.thermalDataToImage(new Uint32Array(await data.arrayBuffer())) : {};
                _pixelConverter = convertedImage.pixelConverter;
                image.src = convertedImage.dataUrl ?? "";
                updateTooltip();
            } else {
                _pixelConverter = undefined;
                image.src = await data.asBase64Url();
            }
            if (!image.src.isEmpty()) firstImageReceived = true;
        });
    };
    export const stopStreaming = function () {
        _hubConnection?.stop();
        _hubConnection = undefined;
        _tooltip?.dispose();
        _tooltip = undefined;
    };
    function updateTooltip() {
        if (_pixelConverter == null || _tooltip == undefined || _lastPointerPosition == null) return;
        const value = _pixelConverter(_lastPointerPosition.offsetX, _lastPointerPosition.offsetY);
        _tooltip.updateFunction(_lastPointerPosition, value.toFixed(2) + " °C");
    }
</script>

<div class="col-md-12 d-flex flex-row">
    {#if currentPosition != undefined}
        <div>Current Position: {currentPosition}</div>
    {/if}
    {#if currentPosition != undefined}
        <div class="ms-3">Image Time: {_currentTime?.toLocaleTimeString()}</div>
    {/if}
    {#if _temperature != undefined}
        <div class="ms-3">Temperature: {_temperature}</div>
    {/if}
    {#if _customPhotoStreaming}
        <div class="ms-3">IR zipped: {_customPhotoStreamProgress.irZipProgress}</div>
        <div class="ms-3">Vis zipped: {_customPhotoStreamProgress.visZipProgress}</div>
        <div class="ms-3">Download: {(_customPhotoStreamProgress.downloadProgress * 100).toFixed(1)}%</div>
    {/if}
</div>
<div class="col-md-12"></div>
<img
    on:mouseenter={(x) => {
        if (_tooltip != undefined || _pixelConverter == null) return;
        _lastPointerPosition = x;
        _tooltip = TooltipCreator.CreateTooltip("", x);
        updateTooltip();
    }}
    on:mouseleave={() => {
        if (_tooltip == undefined) return;
        _tooltip.dispose();
        _tooltip = undefined;
    }}
    on:pointermove={(x) => {
        _lastPointerPosition = x;
        updateTooltip();
    }}
    alt="preview"
    id={_canvasId} />
