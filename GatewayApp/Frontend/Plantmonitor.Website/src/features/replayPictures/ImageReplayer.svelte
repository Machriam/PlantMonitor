<script lang="ts">
    class ImageData {
        date: Date;
        stepCount: number;
        imageUrl: string;
    }
    import {onDestroy, onMount} from "svelte";
    import {CameraType, PictureClient, PictureSeriesData} from "~/services/GatewayAppApi";
    import {selectedDevice} from "../store";
    import {DeviceStreaming} from "~/services/DeviceStreaming";
    import type {HubConnection} from "@microsoft/signalr";

    let pictureSeries: PictureSeriesData[] = [];
    let selectedSeries: PictureSeriesData | undefined;
    let hubConnection: HubConnection | undefined;
    let selectedImage: ImageData | undefined;
    let currentImage: number = -1;
    let images: ImageData[] = [];
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
        connection.start(async (step, date, image) => {
            images.push({imageUrl: image, stepCount: step, date: date});
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
            <img alt="" style="width: 100%;" src={selectedImage?.imageUrl} />
            <div>{selectedImage.date.toLocaleTimeString()}</div>
            <div>Position: {selectedImage.stepCount}</div>
        {/if}
    </div>
</div>
