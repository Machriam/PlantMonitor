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
        PlantExtractionTemplateModel,
        PlantModel,
        type IIrCameraOffset
    } from "~/services/GatewayAppApi";
    import ImageCutter from "./ImageCutter.svelte";
    import {Task} from "~/types/task";
    import TextInput from "../reuseableComponents/TextInput.svelte";
    import {imageToCutChanged, plantPolygonChanged, selectedDevice, selectedPhotoTourPlantInfo} from "../store";
    import type {Unsubscriber} from "svelte/motion";
    import type {ImageToCut} from "./ImageToCut";
    let _availableTours: PhotoTourInfo[] = [];
    let _selectedTour: PhotoTourInfo | undefined;
    let _pictureTrips: PictureTripData[] = [];
    let _selectedTrip: PictureTripData | undefined;
    let _extractionTemplatesOfTrip: PlantExtractionTemplateModel[] = [];
    let _plants: PhotoTourPlantInfo[] = [];
    let _newPlant: PhotoTourPlant = new PhotoTourPlant();
    let _unsubscribe: Unsubscriber[] = [];
    let _selectedImage: ImageToCut | undefined;
    let _baseOffset: IIrCameraOffset = {left: 0, top: 0};

    onDestroy(() => {
        _unsubscribe.map((x) => x());
    });
    onMount(async () => {
        const photoTourClient = new AutomaticPhotoTourClient();
        _availableTours = await photoTourClient.getPhotoTours();
        _unsubscribe.push(
            imageToCutChanged.subscribe(async (x) => {
                _selectedImage = x;
                _plants = _plants;
                if ($selectedPhotoTourPlantInfo != undefined && $selectedPhotoTourPlantInfo.length > 0)
                    $selectedPhotoTourPlantInfo = [];
            })
        );
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
        if (_selectedTour == undefined || _selectedTrip == undefined) return;
        const stitchingClient = new PhotoStitchingClient();
        _extractionTemplatesOfTrip = await stitchingClient.extractionsOfTrip(_selectedTrip.tripId);
        _plants = await stitchingClient.plantsForTour(_selectedTour.id);
    }
    async function selectedTripChanged(data: PictureTripData) {
        const stitchingClient = new PhotoStitchingClient();
        _extractionTemplatesOfTrip = await stitchingClient.extractionsOfTrip(data.tripId);
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
    async function recalculateVirtualPictures() {
        if (_selectedTour == undefined) return;
        const stitchingClient = new PhotoStitchingClient();
        await stitchingClient.recalculatePhotoTour(_selectedTour.id);
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
            class="col-md-1 me-2 p-2 mt-2 alert {_selectedTour?.id == tour.id ? 'bg-info bg-opacity-50' : ''}  border-dark">
            {tour.name}
        </button>
    {/each}
</div>
<div class="row">
    <div style="overflow-y: auto;height:80vh" class="d-flex flex-column col-md-2">
        {#each _pictureTrips as { tripId, timeStamp, irData, visData }, i}
            {@const polyLength = _plants
                .flatMap((p) => p.extractionMetaData.map((et) => et.tripWithExtraction))
                .filter((p) => p == tripId).length}
            <button
                on:click={async () => await selectedTripChanged(_pictureTrips[i])}
                class="row border-secondary border mt-2 {tripId == _selectedTrip?.tripId ? 'bg-info bg-opacity-50' : ''}">
                <div class="col-md-6 p-1">{timeStamp.toLocaleString()}</div>
                <div class="col-md-2 p-1">IR<br /> {irData.count}</div>
                <div class="col-md-2 p-1">VIS {visData.count}</div>
                <div class="col-md-2 p-1">
                    {polyLength > 0 ? "Polys " + polyLength : ""}
                </div>
            </button>
        {/each}
    </div>
    <div class="col-md-8">
        {#if _selectedTrip !== undefined && _selectedTrip !== undefined}
            <ImageCutter
                polygonChanged={updatePlantInfo}
                _extractionTemplates={_extractionTemplatesOfTrip}
                _selectedPhotoTrip={_selectedTrip}
                deviceId={_selectedTrip.deviceId}
                visSeries={_selectedTrip.visData.folderName.getFileName()}
                irSeries={_selectedTrip.irData.folderName.getFileName()}></ImageCutter>
        {/if}
    </div>
    <div class="col-md-2">
        <div class="d-flex flex-row justify-content-center mb-2">
            <button on:click={recalculateVirtualPictures} class="btn btn-primary">Recalculate Virtual Pictures</button>
        </div>
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
            {#if _selectedTrip != undefined}
                {#each _plants as plant}
                    {@const template = _extractionTemplatesOfTrip.find((et) => et.photoTourPlantFk == plant.id)}
                    <button
                        on:click={() => ($selectedPhotoTourPlantInfo = [plant])}
                        class="d-flex flex-column border mb-2 col-md-11 bg-opacity-25
                        {$selectedPhotoTourPlantInfo?.find((p) => p.id == plant.id) != undefined ? 'bg-info' : 'bg-white'}">
                        <div class="d-flex flex-row justify-content-between col-md-12">
                            <div>
                                Pos: {template?.motorPosition}
                            </div>
                            <div>{template?.photoTripFk == _selectedTrip.tripId ? "Poly" : ""}</div>
                        </div>
                        <div
                            style="align-self: center;"
                            class={_extractionTemplatesOfTrip.find((et) => et.photoTourPlantFk == plant.id)?.motorPosition ==
                            _selectedImage?.stepCount
                                ? "fw-bold"
                                : ""}>
                            {plant.name} - {plant.qrCode?.isEmpty() ? "No QR" : plant.qrCode}
                        </div>
                        <div style="align-self: center;">{plant.comment}</div>
                    </button>
                {/each}
            {/if}
        </div>
    </div>
</div>
