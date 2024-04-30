<script lang="ts">
    import {onDestroy, onMount} from "svelte";
    import {PictureClient} from "~/services/GatewayAppApi";
    import {selectedDevice} from "../store";
    import {DeviceStreaming} from "~/services/DeviceStreaming";
    import type {HubConnection} from "@microsoft/signalr";

    let pictureSeries: string[] = [];
    let selectedSeries: string | undefined;
    let hubConnection: HubConnection | undefined;
    let stepCount: number;
    let timestamp: Date;
    let imageUrl: string = "";
    onMount(async () => {
        const pictureClient = new PictureClient();
        if ($selectedDevice == undefined || $selectedDevice?.health.deviceId.isEmpty()) return;
        pictureSeries = await pictureClient.getPictureSeries($selectedDevice?.health.deviceId);
    });
    const subscription = selectedDevice.subscribe(async (x) => {
        const pictureClient = new PictureClient();
        if ($selectedDevice == undefined || $selectedDevice?.health.deviceId.isEmpty()) return;
        pictureSeries = await pictureClient.getPictureSeries($selectedDevice?.health.deviceId);
        pictureSeries = pictureSeries.sort().toReversed();
    });
    function onSeriesSelected(series: string) {
        if ($selectedDevice == undefined || $selectedDevice?.health.deviceId.isEmpty()) return;
        selectedSeries = series;
        const streamer = new DeviceStreaming();
        const connection = streamer.replayPictures($selectedDevice.health.deviceId, series);
        hubConnection?.stop();
        hubConnection = connection.connection;
        connection.start(async (step, date, image) => {
            imageUrl = image;
            timestamp = date;
            stepCount = step;
        });
    }
    onDestroy(() => {
        subscription();
        hubConnection?.stop();
    });
</script>

<div class="col-md-12 row">
    {#each pictureSeries as series}
        <button
            on:click={() => onSeriesSelected(series)}
            style="border-width: 1px;border-style: solid;border-color:grey"
            class="col-md-2 alert ms-2 {selectedSeries == series ? 'alert-info' : ''}">{series}</button>
    {/each}
    <div class="col-md-5">
        {#if !imageUrl.isEmpty()}
            <img alt="bla" style="width: 100%;" src={imageUrl} />
        {/if}
    </div>
    <div class="col-md-5">
        {#if !imageUrl.isEmpty()}
            <img alt="bla" style="width: 100%;" src={imageUrl} />
        {/if}
    </div>
</div>
