<script lang="ts">
    import {onDestroy, onMount} from "svelte";
    import {
        AddPlantModel,
        AutomaticPhotoTourClient,
        PhotoStitchingClient,
        PhotoTourInfo,
        PhotoTourPlant,
        PhotoTourPlantInfo,
        PictureClient,
        PictureTripData,
        PlantModel,
        type IIrCameraOffset
    } from "~/services/GatewayAppApi";
    import ImageCutter from "./ImageCutter.svelte";
    import {Task} from "~/types/task";
    import TextInput from "../reuseableComponents/TextInput.svelte";
    import {plantPolygonChanged, selectedDevice, selectedPhotoTourPlantInfo} from "../store";
    import type {Unsubscriber} from "svelte/motion";
    let _availableTours: PhotoTourInfo[] = [];
    let _selectedTour: PhotoTourInfo | undefined;
    let _pictureTrips: PictureTripData[] = [];
    let _selectedTrip: PictureTripData | undefined;
    let _plants: PhotoTourPlantInfo[] = [];
    let _newPlant: PhotoTourPlant = new PhotoTourPlant();
    let _unsubscribe: Unsubscriber[] = [];

    onDestroy(() => {
        _unsubscribe.map((x) => x());
    });
    onMount(async () => {
        const photoTourClient = new AutomaticPhotoTourClient();
        _availableTours = await photoTourClient.getPhotoTours();
        _unsubscribe.push(
            plantPolygonChanged.subscribe(async (x) => {
                await updatePlantInfo();
            })
        );
        _unsubscribe.push(
            selectedDevice.subscribe(async (x) => {
                _baseOffset = x?.health.cameraOffset ?? {left: 0, top: 0};
            })
        );
    });
    async function selectedTourChanged(newTour: PhotoTourInfo) {
        _selectedTour = newTour;
        const pictureClient = new PictureClient();
        _pictureTrips = await pictureClient.pictureSeriesOfTour(newTour.id);
        const stitchingClient = new PhotoStitchingClient();
        _plants = await stitchingClient.plantsForTour(newTour.id);
        _selectedTrip = undefined;
    }
    async function updatePlantInfo() {
        if (_selectedTour == undefined) return;
        const stitchingClient = new PhotoStitchingClient();
        _plants = await stitchingClient.plantsForTour(_selectedTour.id);
    }
    async function selectedTripChanged(data: PictureTripData) {
        _selectedTrip = undefined;
        await Task.delay(1);
        _selectedTrip = data;
    }
    async function removePlant() {
        if ($selectedPhotoTourPlantInfo == undefined || _selectedTour == undefined) return;
        const stitchingClient = new PhotoStitchingClient();
        await stitchingClient.removePlantsFromTour($selectedPhotoTourPlantInfo.map((p) => p.id));
        _plants = await stitchingClient.plantsForTour(_selectedTour.id);
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
        {#each _pictureTrips as series}
            <button on:click={async () => await selectedTripChanged(series)} class="row border-secondary border mt-2">
                <div class="col-md-6">{series.timeStamp.toLocaleString()}</div>
                <div class="col-md-3">IR: {series.irData.count}</div>
                <div class="col-md-3">VIS: {series.visData.count}</div>
            </button>
        {/each}
    </div>
    <div class="col-md-8">
        {#if _selectedTrip !== undefined && _selectedTrip !== undefined}
            <ImageCutter
                polygonChanged={updatePlantInfo}
                _selectedPhotoTrip={_selectedTrip}
                deviceId={_selectedTrip.deviceId}
                visSeries={_selectedTrip.visData.folderName.getFileName()}
                irSeries={_selectedTrip.irData.folderName.getFileName()}></ImageCutter>
        {/if}
    </div>
    <div class="col-md-2">
        <TextInput label="QR-Code" bind:value={_newPlant.qrCode}></TextInput>
        <TextInput label="Name" bind:value={_newPlant.name}></TextInput>
        <TextInput label="Comment" bind:value={_newPlant.comment}></TextInput>
        <div class="d-flex flex-row justify-content-between mt-2">
            <button on:click={addPlant} class="btn btn-primary">Add Plant</button>
            <button on:click={removePlant} class="btn btn-danger">Remove Plant</button>
        </div>
        <div class="d-flex flex-row justify-content-around mt-2">
            <button on:click={() => ($selectedPhotoTourPlantInfo = _plants)} class="btn btn-primary"
                >Show polygons on image</button>
        </div>
        <div style="overflow-y:auto;height:60vh" class="mt-3">
            {#each _plants as plant}
                <button
                    on:click={() => ($selectedPhotoTourPlantInfo = [plant])}
                    class="d-flex flex-column border mb-2 col-md-11 bg-opacity-25
                        {$selectedPhotoTourPlantInfo?.find((p) => p.id == plant.id) != undefined ? 'bg-info' : 'bg-white'}">
                    <div>Pos: {plant.extractionTemplate.map((et) => et.motorPosition)}</div>
                    <div style="align-self: center;">
                        {plant.name} - {plant.qrCode?.isEmpty() ? "No QR" : plant.qrCode}
                    </div>
                    <div style="align-self: center;">{plant.comment}</div>
                </button>
            {/each}
        </div>
    </div>
</div>
