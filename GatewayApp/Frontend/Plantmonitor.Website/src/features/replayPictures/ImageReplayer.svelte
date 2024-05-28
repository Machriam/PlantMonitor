<script lang="ts">
    class ImageData {
        temperature: number | undefined;
        date: Date;
        stepCount: number;
        imageUrl: string;
        pixelConverter: ((x: number, y: number) => number) | undefined;
    }
    import {onDestroy, onMount} from "svelte";
    import {CameraType, PictureClient, PictureSeriesData} from "~/services/GatewayAppApi";
    import {selectedDevice} from "../store";
    import {DeviceStreaming} from "~/services/DeviceStreaming";
    import type {HubConnection} from "@microsoft/signalr";
    import {CvInterop} from "../deviceConfiguration/CvInterop";
    import {TooltipCreator, type TooltipCreatorResult} from "../reuseableComponents/TooltipCreator";

    let pictureSeries: PictureSeriesData[] = [];
    let selectedSeries: PictureSeriesData | undefined;
    let hubConnection: HubConnection | undefined;
    let selectedImage: ImageData | undefined;
    let currentImage: number = -1;
    let images: ImageData[] = [];
    let lastPointerPosition: MouseEvent | undefined;
    let tooltip: TooltipCreatorResult | undefined;
    const cvInterop = new CvInterop();
    onMount(async () => {
        await updatePictureSeries();
    });
    const subscription = selectedDevice.subscribe(async (x) => {
        await updatePictureSeries();
    });
    async function updatePictureSeries() {
        const pictureClient = new PictureClient();
        if ($selectedDevice == undefined || $selectedDevice?.health.deviceId.isEmpty()) return;
        pictureSeries = await pictureClient.getPictureSeries($selectedDevice?.health.deviceId);
        pictureSeries = pictureSeries.sort((a, b) => a.folderName.localeCompare(b.folderName)).toReversed();
    }
    function onSeriesSelected(series: PictureSeriesData) {
        if ($selectedDevice == undefined || $selectedDevice?.health.deviceId.isEmpty()) return;
        selectedSeries = series;
        const streamer = new DeviceStreaming();
        const connection = streamer.replayPictures($selectedDevice.health.deviceId, series.folderName);
        hubConnection?.stop();
        hubConnection = connection.connection;
        images = [];
        currentImage = -1;
        connection.start(async (step, date, image, temperature) => {
            let dataUrl = "";
            let pixelConverter = undefined;
            if (series.type == CameraType.IR) {
                const convertedImage = cvInterop.thermalDataToImage(new Uint32Array(await image.arrayBuffer()));
                pixelConverter = convertedImage.pixelConverter;
                dataUrl = convertedImage.dataUrl ?? "";
            } else {
                dataUrl = await image.asBase64Url();
            }
            images.push({
                imageUrl: dataUrl,
                stepCount: step,
                date: date,
                temperature: temperature,
                pixelConverter: pixelConverter
            });
            currentImage = images.length - 1;
            selectedImage = images[currentImage];
        });
    }
    function onScroll(event: WheelEvent) {
        if (currentImage == -1) return;
        if (event.deltaY < 0 && currentImage > 0) {
            currentImage = currentImage - 1;
        } else if (event.deltaY > 0 && currentImage < images.length - 1) {
            currentImage = currentImage + 1;
        }
        selectedImage = images[currentImage];
        updateTooltip();
    }
    function updateTooltip() {
        if (selectedImage?.pixelConverter == null || tooltip == undefined || lastPointerPosition == null) return;
        const value = selectedImage.pixelConverter(lastPointerPosition.offsetX, lastPointerPosition.offsetY);
        tooltip.updateFunction(lastPointerPosition, value.toFixed(2) + " °C");
    }
    onDestroy(() => {
        subscription();
        hubConnection?.stop();
    });
</script>

<div class={$$restProps.class || ""}>
    <div style="height: 10vh;overflow-y:auto;text-align-last:left" class="d-flex flex-column col-md-4">
        {#each pictureSeries as series}
            <button
                on:click={() => onSeriesSelected(series)}
                style="border-width: 1px;border-style: solid;border-color:grey"
                class="col-md-12 row alert border-0 m-0 p-0 {selectedSeries == series ? 'alert-info' : ''}">
                <span class="col-md-2">{series.count}</span>
                <span class="col-md-1">{series.type == CameraType.IR ? "IR" : "Vis"}</span>
                <span class="col-md-9">{series.folderName}</span>
            </button>
        {/each}
    </div>
    <div on:wheel={(x) => onScroll(x)}>
        {#if selectedImage != undefined}
            <img
                on:mouseenter={(x) => {
                    if (tooltip != undefined || selectedImage?.pixelConverter == null) return;
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
                width={selectedImage.pixelConverter == undefined ? "100%" : ""}
                src={selectedImage?.imageUrl} />
            <div>{selectedImage.date.toLocaleTimeString()}</div>
            <div>Position: {selectedImage.stepCount}</div>
            {#if selectedImage.temperature != undefined}
                <div>Temperature: {selectedImage.temperature}</div>
            {/if}
        {/if}
    </div>
</div>
