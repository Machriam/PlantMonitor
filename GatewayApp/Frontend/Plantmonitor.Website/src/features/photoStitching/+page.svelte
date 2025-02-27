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
    import {Task} from "~/types/Task";
    import TextInput from "../reuseableComponents/TextInput.svelte";
    import {imageToCutChanged, plantPolygonChanged, selectedPhotoTourPlantInfo} from "./PhotoStitchingContext";
    import type {Unsubscriber} from "svelte/motion";
    import type {ImageToCut} from "./ImageToCut";
    import IrFineAdjustment from "./IrFineAdjustment.svelte";
    import {selectedDevice} from "../store";
    import {pipe} from "~/types/Pipe";
    let _availableTours: PhotoTourInfo[] = [];
    let _selectedTour: PhotoTourInfo | undefined;
    let _pictureTrips: PictureTripData[] = [];
    let _selectedTrip: PictureTripData | undefined;
    let _extractionTemplatesOfTrip: PlantExtractionTemplateModel[] = [];
    let _plants: PhotoTourPlantInfo[] = [];
    let _newPlant: PhotoTourPlant = new PhotoTourPlant();
    let _unsubscribe: Unsubscriber[] = [];
    let _selectedImage: ImageToCut | undefined;
    let _warningShown = false;
    let _baseOffset: IIrCameraOffset = {left: 0, top: 0};
    let _uniqueId = Math.random().toString(36).substring(7);

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
    async function onPlantSelected(plant: PhotoTourPlantInfo) {
        $selectedPhotoTourPlantInfo = [plant];
        if (_warningShown) return;
        _warningShown =
            $selectedDevice?.health.cameraOffset != undefined &&
            $selectedDevice?.health.cameraOffset?.left != 0 &&
            $selectedDevice?.health.cameraOffset?.top != 0;
        if (_warningShown) return;
        alert("Select a device with defined global alignment for proper ir preview.");
        _warningShown = true;
    }
    async function addPlant() {
        if (_selectedTour == undefined) return;
        const stitchingClient = new PhotoStitchingClient();
        const plants = [
            new PlantModel({
                comment: _newPlant.comment,
                name: _newPlant.name,
                position: _newPlant.position ?? ""
            })
        ];
        await stitchingClient.addPlantsToTour(new AddPlantModel({plants: plants, tourId: _selectedTour.id}));
        _plants = await stitchingClient.plantsForTour(_selectedTour.id);
    }
    async function nextPlant(direction: number) {
        const plantsToTakeFrom = _plants
            .map((plant) => ({plant: plant, template: _extractionTemplatesOfTrip.find((et) => et.photoTourPlantFk == plant.id)}))
            .filter((et) => et?.template?.motorPosition == _selectedImage?.stepCount)
            .map((et) => et.plant);
        if ($selectedPhotoTourPlantInfo == undefined || $selectedPhotoTourPlantInfo?.length == 0)
            $selectedPhotoTourPlantInfo = [plantsToTakeFrom[0]];
        else {
            const plantToFind = $selectedPhotoTourPlantInfo[0];
            const index = plantsToTakeFrom.findIndex((x) => x.id == plantToFind.id);
            let newIndex = index + direction;
            if (newIndex < 0) newIndex = plantsToTakeFrom.length - 1;
            if (newIndex >= plantsToTakeFrom.length) newIndex = 0;
            $selectedPhotoTourPlantInfo = [plantsToTakeFrom[newIndex]];
        }
        const plantButton = document.getElementById(_uniqueId + $selectedPhotoTourPlantInfo[0].id);
        plantButton?.scrollIntoView({behavior: "instant", inline: "nearest", block: "nearest"});
    }
</script>

<svelte:head><title>Photo Stitching</title></svelte:head>

<h3>Available Tours</h3>
<div style="width: 80vw; overflow-x: auto;" class="d-flex flex-row">
    {#each _availableTours.toSorted((t1, t2) => (t1.lastEvent <= t2.lastEvent ? 1 : -1)) as tour}
        <button
            on:click={async () => await selectedTourChanged(tour)}
            class="col-md-1 me-2 p-2 mt-2 alert {_selectedTour?.id == tour.id ? 'bg-info bg-opacity-50' : ''}  border-dark">
            {tour.name}
        </button>
    {/each}
</div>
<div class="row">
    <div style="overflow-y: auto;height:70vh" class="d-flex flex-column col-md-2">
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
                visSeries={pipe(_selectedTrip.visData.folderName).getFileName()}
                irSeries={pipe(_selectedTrip.irData.folderName).getFileName()}></ImageCutter>
        {/if}
    </div>
    <div class="col-md-2">
        <div class="d-flex flex-row justify-content-center mb-2">
            <button disabled={_selectedTour == undefined} on:click={recalculateVirtualPictures} class="btn btn-primary"
                >Recalculate Virtual Pictures</button>
        </div>
        <TextInput label="Position" bind:value={_newPlant.position}></TextInput>
        <TextInput label="Name" bind:value={_newPlant.name}></TextInput>
        <TextInput label="Comment" bind:value={_newPlant.comment}></TextInput>
        <div class="d-flex flex-row justify-content-between mt-2">
            <button disabled={_selectedTour == undefined} on:click={addPlant} class="btn btn-primary">Add Plant</button>
            <button disabled={_selectedTour == undefined} on:click={removePlant} class="btn btn-danger">Remove Plant</button>
        </div>
        <div class="d-flex flex-row justify-content-around mt-2">
            <button
                disabled={_selectedTrip == undefined}
                on:click={() => ($selectedPhotoTourPlantInfo = _plants)}
                class="btn btn-primary">Show polygons on image</button>
        </div>
        <div style="overflow-y:auto;height:40vh" class="mt-3">
            {#if _selectedTrip != undefined}
                {#each _plants as plant}
                    {@const template = _extractionTemplatesOfTrip.find((et) => et.photoTourPlantFk == plant.id)}
                    <button
                        id="{_uniqueId}{plant.id}"
                        on:click={() => onPlantSelected(plant)}
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
                            class={template?.motorPosition == _selectedImage?.stepCount ? "fw-bold" : ""}>
                            {plant.name} - {pipe(plant.position ?? "").isEmpty() ? "No Pos." : plant.position}
                        </div>
                        <div style="align-self: center;">{plant.comment}</div>
                    </button>
                {/each}
            {/if}
        </div>
        <div class="col-md-12 row justify-content-between mt-2">
            <button
                disabled={_selectedTrip == undefined}
                on:click={() => nextPlant(-1)}
                style="font-size:40px;width:50px;height:30px;line-height:0px"
                class="btn btn-dark p-0">
                <div style="position:relative; top:-3px">&leftarrow;</div>
            </button>
            <button
                disabled={_selectedTrip == undefined}
                on:click={() => nextPlant(1)}
                style="font-size:40px;width:50px;height:30px;line-height:0px"
                class="btn btn-dark p-0">
                <div style="position:relative; top:-3px">&rightarrow;</div>
            </button>
        </div>
    </div>
</div>
<hr class="m-3" />
{#if _selectedTrip != undefined}
    <IrFineAdjustment bind:_extractionTemplates={_extractionTemplatesOfTrip} bind:_selectedTrip></IrFineAdjustment>
{/if}
