<script lang="ts">
    import {onDestroy, onMount} from "svelte";
    import {AutomaticPhotoTourClient, PhotoTourInfo, PictureClient, PictureSeriesTourData} from "~/services/GatewayAppApi";
    import ImageCutter from "./ImageCutter.svelte";
    let availableTours: PhotoTourInfo[] = [];
    let selectedTour: PhotoTourInfo | undefined;
    let pictureSeries: PictureSeriesTourData[] = [];
    let selectedSeries: PictureSeriesTourData | undefined;

    onDestroy(() => {});
    onMount(async () => {
        const photoTourClient = new AutomaticPhotoTourClient();
        availableTours = await photoTourClient.getPhotoTours();
    });
    async function selectedTourChanged(newTour: PhotoTourInfo) {
        selectedTour = newTour;
        const pictureClient = new PictureClient();
        pictureSeries = await pictureClient.pictureSeriesOfTour(newTour.id);
    }
    async function selectedPhotoSeriesChanged(data: PictureSeriesTourData) {
        selectedSeries = data;
    }
</script>

<h3>Available Tours</h3>
<div class="d-flex flex-row">
    {#each availableTours as tour}
        <button
            on:click={async () => await selectedTourChanged(tour)}
            class="col-md-1 me-2 p-2 mt-2 alert {selectedTour === tour ? 'bg-info bg-opacity-50' : ''}  border-dark">
            {tour.name}
        </button>
    {/each}
</div>
<div class="row">
    <div class="d-flex flex-column col-md-2">
        {#each pictureSeries as series}
            <button on:click={async () => await selectedPhotoSeriesChanged(series)} class="row border-secondary border mt-2">
                <div class="col-md-6">{series.timeStamp.toLocaleString()}</div>
                <div class="col-md-3">IR: {series.irData.count}</div>
                <div class="col-md-3">VIS: {series.visData.count}</div>
            </button>
        {/each}
    </div>
    <div class="col-md-8">
        {#if selectedSeries !== undefined}
            <ImageCutter
                deviceId={selectedSeries.deviceId}
                visSeries={selectedSeries.visData.folderName.getFileName()}
                irSeries={selectedSeries.irData.folderName.getFileName()}></ImageCutter>
        {/if}
    </div>
</div>
