<script lang="ts">
    import {
        ImageCropPreview,
        IrOffsetFineAdjustment,
        NpgsqlPoint,
        PhotoStitchingClient,
        PlantExtractionTemplateModel,
        type PictureTripData
    } from "~/services/GatewayAppApi";
    import {selectedPhotoTourPlantInfo} from "../store";
    import {IrScalingHeight, IrScalingWidth} from "../deviceConfiguration/CvInterop";
    import {Task} from "~/types/task";
    import {onMount} from "svelte";
    export let _selectedTrip: PictureTripData | undefined;
    export let _extractionTemplates: PlantExtractionTemplateModel[] = [];
    let _imageCropPreview: ImageCropPreview | undefined;
    let _opacity = 1;
    let _xOffset = 0;
    let _yOffset = 0;
    let _availableExtractionTemplates: NpgsqlPoint[] = [];
    let _polygonUpdater: () => void;
    let _selectedTemplate: NpgsqlPoint | undefined;
    onMount(() => {
        _polygonUpdater = Task.createDebouncer(displayPolygonIrOffset, 150);
    });
    function keyPressed(evt: KeyboardEvent) {
        if (evt.key == "ArrowRight") {
            _xOffset += 1;
            evt.preventDefault();
        }
        if (evt.key == "ArrowLeft") {
            _xOffset -= 1;
            evt.preventDefault();
        }
        if (evt.key == "ArrowUp") {
            _yOffset -= 1;
            evt.preventDefault();
        }
        if (evt.key == "ArrowDown") {
            _yOffset += 1;
            evt.preventDefault();
        }
        _polygonUpdater();
    }
    async function loadNewPolygon() {
        if (_extractionTemplates.length == 0 || _selectedTrip == undefined || $selectedPhotoTourPlantInfo == undefined) return;
        document.removeEventListener("keydown", keyPressed);
        document.addEventListener("keydown", keyPressed);
        const extractionTemplateId = _extractionTemplates.find(
            (et) => et.photoTourPlantFk == $selectedPhotoTourPlantInfo[0].id
        )?.id;
        const client = new PhotoStitchingClient();
        const result = await client.croppedImageFor(extractionTemplateId, _selectedTrip.tripId, 0, 0);
        _xOffset = 0;
        _yOffset = 0;
        _selectedTemplate == undefined;
        return result;
    }
    async function displayPolygonIrOffset() {
        if (_extractionTemplates.length == 0 || _selectedTrip == undefined || $selectedPhotoTourPlantInfo == undefined) return;
        const extractionTemplateId = _extractionTemplates.find(
            (et) => et.photoTourPlantFk == $selectedPhotoTourPlantInfo[0].id
        )?.id;
        const client = new PhotoStitchingClient();
        _imageCropPreview = await client.croppedImageFor(extractionTemplateId, _selectedTrip.tripId, _xOffset, _yOffset);
    }
    function uniqueExtractionOffsets() {
        const offsets = _extractionTemplates.map((et) => JSON.stringify(et.irBoundingBoxOffset));
        return [...new Set(offsets)].map((o) => NpgsqlPoint.fromJS(JSON.parse(o))).toSorted((a, b) => a.x - b.x);
    }
    async function storePolygonIrOffset() {
        if (_extractionTemplates.length == 0 || _selectedTrip == undefined || $selectedPhotoTourPlantInfo == undefined) return;
        const extractionTemplateId = _extractionTemplates.find(
            (et) => et.photoTourPlantFk == $selectedPhotoTourPlantInfo[0].id
        )?.id;
        if (extractionTemplateId == undefined || _imageCropPreview == undefined) return;
        const client = new PhotoStitchingClient();
        await client.updateIrOffset(
            new IrOffsetFineAdjustment({
                extractionTemplateId: extractionTemplateId,
                newIrOffset: _imageCropPreview.currentOffset
            })
        );
        _imageCropPreview = await loadNewPolygon();
    }
</script>

<div style="height:80vh" class="col-md-12 d-flex flex-column">
    {#if $selectedPhotoTourPlantInfo?.length != undefined && $selectedPhotoTourPlantInfo.length > 0}
        <div>
            Fine adjustement for {$selectedPhotoTourPlantInfo[0].name}
            {$selectedPhotoTourPlantInfo[0].comment} - {$selectedPhotoTourPlantInfo[0].position}
        </div>
        <div class="col-md-12 row">
            <button
                on:click={async () => {
                    _imageCropPreview = await loadNewPolygon();
                    _availableExtractionTemplates = uniqueExtractionOffsets();
                }}
                class="btn btn-primary col-md-2">Show Polygon</button>
            <button on:click={() => storePolygonIrOffset()} class="btn btn-success col-md-2">Upate Offset</button>
        </div>
    {/if}
    {#if _imageCropPreview != undefined}
        <div>
            Current Offset (X,Y): {_imageCropPreview.currentOffset.x.toFixed(1)},
            {_imageCropPreview.currentOffset.y.toFixed(1)}
        </div>
        <div class="col-md-12 row" style="z-index: 0;">
            <div
                class="col-md-6"
                style="position: relative;"
                on:wheel={(evt) => {
                    _opacity = (_opacity + 1) % 2;
                    evt.preventDefault();
                }}>
                <img
                    style="position: absolute;height:{IrScalingHeight}px;left:0px;top:0px"
                    src="data:image/png;base64,{_imageCropPreview.irImage}"
                    alt="IR Crop" />
                <img
                    style="opacity:{_opacity};position: absolute;height:{IrScalingHeight}px;left:0px;top:0px"
                    src="data:image/png;base64,{_imageCropPreview.visImage}"
                    alt="Vis Crop" />
            </div>
            <div class="col-md-4" style="z-index: 1;"></div>
            <div class="col-md-2 d-flex flex-column colm-3" style="height: 40vh;overflow-y:auto;z-index:1">
                {#each _availableExtractionTemplates as template}
                    <button
                        on:click={async () => {
                            const defaultPoly = await loadNewPolygon();
                            if (defaultPoly == undefined) return;
                            _xOffset = defaultPoly.currentOffset.x - template.x;
                            _yOffset = defaultPoly.currentOffset.y - template.y;
                            _selectedTemplate = template;
                            await displayPolygonIrOffset();
                            _availableExtractionTemplates = _availableExtractionTemplates;
                        }}
                        class="btn btn-dark {_selectedTemplate == template ||
                        (_imageCropPreview.currentOffset.x == template.x && _imageCropPreview.currentOffset.y == template.y)
                            ? ''
                            : 'opacity-50'}">
                        Try Offset (X,Y): {template.x.toFixed(1)}, {template.y.toFixed(1)}</button>
                {/each}
            </div>
        </div>
    {/if}
</div>
