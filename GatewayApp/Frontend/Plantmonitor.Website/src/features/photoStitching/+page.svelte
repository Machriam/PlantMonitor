<script lang="ts">
    import {onDestroy, onMount} from "svelte";
    import {
        AddPlantModel,
        AutomaticPhotoTourClient,
        PhotoStitchingClient,
        PhotoTourInfo,
        PhotoTourPlant,
        PictureClient,
        PictureSeriesTourData,
        PlantModel
    } from "~/services/GatewayAppApi";
    import ImageCutter from "./ImageCutter.svelte";
    import {Task} from "~/types/task";
    import TextInput from "../reuseableComponents/TextInput.svelte";
    let _availableTours: PhotoTourInfo[] = [];
    let _selectedTour: PhotoTourInfo | undefined;
    let _pictureSeries: PictureSeriesTourData[] = [];
    let _selectedSeries: PictureSeriesTourData | undefined;
    let _plants: PhotoTourPlant[] = [];
    let _newPlant: PhotoTourPlant = new PhotoTourPlant();

    onDestroy(() => {});
    onMount(async () => {
        const photoTourClient = new AutomaticPhotoTourClient();
        _availableTours = await photoTourClient.getPhotoTours();
    });
    async function selectedTourChanged(newTour: PhotoTourInfo) {
        _selectedTour = newTour;
        const pictureClient = new PictureClient();
        _pictureSeries = await pictureClient.pictureSeriesOfTour(newTour.id);
        const stitchingClient = new PhotoStitchingClient();
        _plants = await stitchingClient.plantsForTour(newTour.id);
        _selectedSeries = undefined;
    }
    async function selectedPhotoSeriesChanged(data: PictureSeriesTourData) {
        _selectedSeries = undefined;
        await Task.delay(1);
        _selectedSeries = data;
    }
    async function addPlant() {
        if (_selectedTour == undefined) return;
        const stitchingClient = new PhotoStitchingClient();
        const plants = [
            new PlantModel({
                comment: _newPlant.comment,
                name: _newPlant.name,
                qrCode: _newPlant.qrCode ?? ""
            })
        ];
        await stitchingClient.addPlantsToTour(new AddPlantModel({plants: plants, tourId: _selectedTour.id}));
        _plants = await stitchingClient.plantsForTour(_selectedTour.id);
    }
</script>

<h3>Available Tours</h3>
<div class="d-flex flex-row">
    {#each _availableTours as tour}
        <button
            on:click={async () => await selectedTourChanged(tour)}
            class="col-md-1 me-2 p-2 mt-2 alert {_selectedTour === tour ? 'bg-info bg-opacity-50' : ''}  border-dark">
            {tour.name}
        </button>
    {/each}
</div>
<div class="row">
    <div style="overflow-y: auto;height:80vh" class="d-flex flex-column col-md-2">
        {#each _pictureSeries as series}
            <button on:click={async () => await selectedPhotoSeriesChanged(series)} class="row border-secondary border mt-2">
                <div class="col-md-6">{series.timeStamp.toLocaleString()}</div>
                <div class="col-md-3">IR: {series.irData.count}</div>
                <div class="col-md-3">VIS: {series.visData.count}</div>
            </button>
        {/each}
    </div>
    <div class="col-md-8">
        {#if _selectedSeries !== undefined}
            <ImageCutter
                deviceId={_selectedSeries.deviceId}
                visSeries={_selectedSeries.visData.folderName.getFileName()}
                irSeries={_selectedSeries.irData.folderName.getFileName()}></ImageCutter>
        {/if}
    </div>
    <div class="col-md-2">
        <TextInput label="QR-Code" bind:value={_newPlant.qrCode}></TextInput>
        <TextInput label="Name" bind:value={_newPlant.name}></TextInput>
        <TextInput label="Comment" bind:value={_newPlant.comment}></TextInput>
        <button on:click={addPlant} class="btn btn-primary">Add Plant</button>
        <div>
            {#each _plants as plant}
                <div>{plant.name}</div>
                <div>{plant.comment}</div>
                <div>{plant.qrCode}</div>
            {/each}
        </div>
    </div>
</div>
