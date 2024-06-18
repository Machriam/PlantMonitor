<script lang="ts">

    import {onDestroy, onMount} from "svelte";
    import {CameraType, PictureClient, PictureSeriesData, SeriesByDevice} from "~/services/GatewayAppApi";
    import {selectedDevice} from "../store";
    import {DeviceStreaming} from "~/services/DeviceStreaming";
    import type {HubConnection} from "@microsoft/signalr";
    import {CvInterop} from "../deviceConfiguration/CvInterop";
    import {TooltipCreator, type TooltipCreatorResult} from "../reuseableComponents/TooltipCreator";
    import Select from "../reuseableComponents/Select.svelte";
    import {resizeBase64Img} from "./ImageResizer";
    import type { ReplayedImage } from "./ReplayedImage";

    let pictureSeries: PictureSeriesData[] = [];
    export const getSelectedImage = () => selectedImage;
    let selectedSeries: PictureSeriesData | undefined;
    let hubConnection: HubConnection | undefined;
    let selectedImage: ReplayedImage | undefined;
    let currentImage: number = -1;
    let images: ReplayedImage[] = [];
    let lastPointerPosition: MouseEvent | undefined;
    let tooltip: TooltipCreatorResult | undefined;
    let seriesByDevice: SeriesByDevice[] = [];
    let selectedDeviceId: string | undefined;
    const cvInterop = new CvInterop();
    onMount(async () => {
        await updatePictureSeries($selectedDevice?.health.deviceId);
    });
    const subscription = selectedDevice.subscribe(async (x) => {
        await updatePictureSeries($selectedDevice?.health.deviceId);
    });
    async function updatePictureSeries(deviceId: string | undefined) {
        const pictureClient = new PictureClient();
        seriesByDevice = await pictureClient.getAllPicturedDevices();
        selectedDeviceId = deviceId;
        if (deviceId == undefined) {
            pictureSeries = [];
            return;
        }
        pictureSeries = await pictureClient.getPictureSeries(deviceId);
        pictureSeries = pictureSeries.sort((a, b) => a.folderName.localeCompare(b.folderName)).toReversed();
    }
    function onSeriesSelected(series: PictureSeriesData) {
        if (selectedDeviceId == undefined) return;
        selectedSeries = series;
        const streamer = new DeviceStreaming();
        const connection = streamer.replayPictures(selectedDeviceId, series.folderName);
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
            const thumbnail = await resizeBase64Img(dataUrl, 100, 100);
            images.push({
                imageUrl: dataUrl,
                stepCount: step,
                date: date,
                temperature: temperature,
                thumbnailUrl: thumbnail,
                pixelConverter: pixelConverter
            });
            if (images.length == 1) {
                currentImage = 0;
                selectedImage = images[currentImage];
            }
            images = images;
        });
    }
    function onScroll(event: WheelEvent) {
        if (currentImage == -1) return;
        let currentIndex = currentImage;
        if (event.deltaY < 0 && currentIndex > 0) {
            currentIndex = currentIndex - 1;
        } else if (event.deltaY > 0 && currentIndex < images.length - 1) {
            currentIndex = currentIndex + 1;
        }
        changeImage(currentIndex);
    }
    function changeImage(newIndex: number) {
        currentImage = newIndex;
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
    <Select
        initialSelectedItem={$selectedDevice?.health.deviceId}
        idSelector={(x) => x.deviceId}
        textSelector={(x) => x.deviceId}
        selectedItemChanged={(x) => updatePictureSeries(x?.deviceId)}
        items={seriesByDevice}
        class="col-md-6"></Select>
    <div style="height: 10vh;overflow-y:auto;text-align-last:left" class="d-flex flex-column col-md-12">
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
    <div class="col-md-12 row p-0" style="min-height: 120px;" on:wheel={(x) => onScroll(x)}>
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
                style="width:{selectedImage.pixelConverter != undefined ? 'initial' : ''}"
                src={selectedImage?.imageUrl} />
            <div class="col-md-3">
                <div>{selectedImage.date.toLocaleTimeString()}</div>
                <div>Position: {selectedImage.stepCount}</div>
                <div>Image {currentImage + 1}/{selectedSeries?.count}</div>
                {#if selectedImage.temperature != undefined && selectedImage.temperature > 0}
                    <div>Temperature: {selectedImage.temperature}</div>
                {/if}
            </div>
            <div style="overflow-x:auto;width:40vw;flex-flow:nowrap;min-height:120px" class="row p-0">
                {#each images as image, i}
                    <div style="height: 80px;width:70px">
                        <button class="p-0 m-0" on:click={() => changeImage(i)} style="height: 70px;width:70px;border:unset">
                            <img style="height: 100%;width:100%" alt="visual scrollbar" src={image.thumbnailUrl} />
                        </button>
                        <div style="font-weight: {i == currentImage ? '700' : '400'};">{i + 1}</div>
                    </div>
                {/each}
            </div>
        {/if}
    </div>
</div>
