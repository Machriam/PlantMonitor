<script lang="ts">
    import {
        ImageCropPreview,
        IrOffsetFineAdjustment,
        NpgsqlPoint,
        PhotoStitchingClient,
        PlantExtractionTemplateModel,
        type PictureTripData
    } from "~/services/GatewayAppApi";
    import {selectedPhotoTourPlantInfo} from "./PhotoStitchingContext";
    import {IrScalingHeight, IrScalingWidth} from "../deviceConfiguration/CvInterop";
    import {Task} from "~/types/Task";
    import {onMount} from "svelte";
    import type {Unsubscriber} from "svelte/motion";
    import {pipe} from "~/types/Pipe";
    import {selectedDevice} from "../store";
    export let _selectedTrip: PictureTripData | undefined;
    export let _extractionTemplates: PlantExtractionTemplateModel[] = [];
    let _imageCropPreview: ImageCropPreview | undefined | null;
    let _opacity = 1;
    let _xOffset = 0;
    let _yOffset = 0;
    let _availableExtractionTemplates: NpgsqlPoint[] = [];
    let _polygonUpdater: () => void;
    let _selectedTemplate: NpgsqlPoint | undefined;
    let _unsubscribe: Unsubscriber[] = [];
    onMount(() => {
        _polygonUpdater = Task.createDebouncer(displayPolygonIrOffset, 150);
        _unsubscribe.push(
            selectedPhotoTourPlantInfo.subscribe(async (value) => {
                _imageCropPreview = await loadNewPolygon();
                _availableExtractionTemplates = uniqueExtractionOffsets();
            })
        );
    });
    function keyPressed(evt: KeyboardEvent) {
        const target = evt.target as HTMLElement;
        if (target.tagName == "INPUT" || target.tagName == "TEXTAREA") return;
        if (evt.key == "ArrowRight") {
            _xOffset += 1;
            evt.preventDefault();
            _polygonUpdater();
        }
        if (evt.key == "ArrowLeft") {
            _xOffset -= 1;
            evt.preventDefault();
            _polygonUpdater();
        }
        if (evt.key == "ArrowUp") {
            _yOffset -= 1;
            evt.preventDefault();
            _polygonUpdater();
        }
        if (evt.key == "ArrowDown") {
            _yOffset += 1;
            evt.preventDefault();
            _polygonUpdater();
        }
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
        if (
            _extractionTemplates.length == 0 ||
            _selectedTrip == undefined ||
            $selectedPhotoTourPlantInfo == undefined ||
            $selectedPhotoTourPlantInfo.length == 0
        )
            return;
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
        {@const position = pipe($selectedPhotoTourPlantInfo[0].position ?? "").isEmpty()
            ? ""
            : "- " + $selectedPhotoTourPlantInfo[0].position}
        <h4>Fine IR adjustment for {$selectedPhotoTourPlantInfo[0].name} {$selectedPhotoTourPlantInfo[0].comment} {position}</h4>
        <div class="col-md-12 row">
            <button
                on:click={async () => {
                    _imageCropPreview = await loadNewPolygon();
                    _availableExtractionTemplates = uniqueExtractionOffsets();
                }}
                class="btn btn-primary col-md-2">Reload Polygon</button>
            <button on:click={() => storePolygonIrOffset()} class="btn btn-success col-md-2 ms-2">Update Offset</button>
            <button
                on:click={async () => {
                    const defaultPoly = await loadNewPolygon();
                    if (defaultPoly == undefined) return;
                    _xOffset = (defaultPoly?.previousOffset.x ?? 0) - defaultPoly.currentOffset.x;
                    _yOffset = (defaultPoly?.previousOffset.y ?? 0) - defaultPoly.currentOffset.y;
                    _selectedTemplate = defaultPoly?.previousOffset;
                    await displayPolygonIrOffset();
                    _availableExtractionTemplates = _availableExtractionTemplates;
                }}
                class="btn btn-dark col-md-3 ms-2">
                Previous Offset (X,Y): {_imageCropPreview?.previousOffset.x.toFixed(1)}, {_imageCropPreview?.previousOffset.y.toFixed(
                    1
                )}</button>
            <button
                class="btn btn-dark col-md-3 ms-2"
                on:click={async () => {
                    _xOffset = $selectedDevice?.health.cameraOffset?.left ?? 0;
                    _yOffset = $selectedDevice?.health.cameraOffset?.top ?? 0;
                    _polygonUpdater();
                }}
                >Global Offset (X,Y): {$selectedDevice?.health.cameraOffset?.left ?? 0}
                {$selectedDevice?.health.cameraOffset?.top ?? 0}</button>
        </div>
    {/if}
    {#if _imageCropPreview != undefined && _imageCropPreview != null}
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
            <div class="col-md-2 d-flex flex-column colm-3" style="height: 480px;overflow-y:auto;z-index:1"></div>
        </div>
    {/if}
</div>
