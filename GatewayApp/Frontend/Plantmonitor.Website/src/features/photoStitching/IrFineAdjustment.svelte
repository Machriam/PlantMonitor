<script lang="ts">
    import {
        ImageCropPreview,
        IrOffsetFineAdjustement,
        NpgsqlPoint,
        PhotoStitchingClient,
        PlantExtractionTemplateModel,
        type PictureTripData
    } from "~/services/GatewayAppApi";
    import {selectedPhotoTourPlantInfo} from "../store";
    import {IrScalingHeight, IrScalingWidth} from "../deviceConfiguration/CvInterop";
    export let _selectedTrip: PictureTripData | undefined;
    export let _extractionTemplates: PlantExtractionTemplateModel[] = [];
    let _imageCropPreview: ImageCropPreview | undefined;
    let _opacity = 1;
    let _xOffset = 0;
    let _yOffset = 0;
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
        console.log({_xOffset, _yOffset});
    }
    async function UpdateCrop() {
        if (_extractionTemplates.length == 0 || _selectedTrip == undefined || $selectedPhotoTourPlantInfo == undefined) return;
        document.removeEventListener("keydown", keyPressed);
        document.addEventListener("keydown", keyPressed);
        const extractionTemplateId = _extractionTemplates.find(
            (et) => et.photoTourPlantFk == $selectedPhotoTourPlantInfo[0].id
        )?.id;
        const client = new PhotoStitchingClient();
        _imageCropPreview = await client.croppedImageFor(extractionTemplateId, _selectedTrip.tripId);
        _xOffset = 0;
        _yOffset = 0;
    }
    function uniqueExtractionOffsets() {
        const offsets = _extractionTemplates.map((et) => JSON.stringify(et.irBoundingBoxOffset));
        return [...new Set(offsets)].map((o) => NpgsqlPoint.fromJS(JSON.parse(o))).toSorted((a, b) => a.x - b.x);
    }
    async function UpdateFineAdjustement(overwriteOffset: boolean, xOffset: number, yOffset: number) {
        if (_extractionTemplates.length == 0 || _selectedTrip == undefined || $selectedPhotoTourPlantInfo == undefined) return;
        const extractionTemplateId = _extractionTemplates.find(
            (et) => et.photoTourPlantFk == $selectedPhotoTourPlantInfo[0].id
        )?.id;
        if (extractionTemplateId == undefined || _imageCropPreview == undefined) return;
        const xRatio = _imageCropPreview?.irImageSize.x / IrScalingHeight;
        const yRatio = _imageCropPreview?.irImageSize.y / IrScalingHeight;
        if (!overwriteOffset) {
            xOffset = (xOffset * xRatio) / 2;
            yOffset = (yOffset * yRatio) / 4;
        }
        const client = new PhotoStitchingClient();
        await client.updateIrOFfset(
            new IrOffsetFineAdjustement({
                extractionTemplateId: extractionTemplateId,
                newIrOffset: new NpgsqlPoint({x: xOffset, y: yOffset}),
                overwriteOffset: overwriteOffset
            })
        );
        await UpdateCrop();
    }
</script>

<div style="height:80vh" class="col-md-12 d-flex flex-column">
    {#if $selectedPhotoTourPlantInfo?.length != undefined && $selectedPhotoTourPlantInfo.length > 0}
        <div>
            Fine adjustement for {$selectedPhotoTourPlantInfo[0].name}
            {$selectedPhotoTourPlantInfo[0].comment} - {$selectedPhotoTourPlantInfo[0].qrCode}
        </div>
        <div class="col-md-12 row">
            <button on:click={UpdateCrop} class="btn btn-primary col-md-2">Show Polygon</button>
            <button on:click={() => UpdateFineAdjustement(false, _xOffset, _yOffset)} class="btn btn-success col-md-2"
                >Upate Offset</button>
        </div>
    {/if}
    {#if _imageCropPreview != undefined}
        <div>Current Offset (X,Y): {_imageCropPreview.currentOffset.x} {_imageCropPreview.currentOffset.y}</div>
        <div class="col-md-12 row">
            <div
                class="col-md-6"
                style="position: relative;"
                on:wheel={(evt) => {
                    _opacity = (_opacity + 1) % 2;
                    evt.preventDefault();
                }}>
                <img
                    style="position: absolute;height:{IrScalingHeight}px;left:{_xOffset}px;top:{_yOffset}px"
                    src="data:image/png;base64,{_imageCropPreview.irImage}"
                    alt="IR Crop" />
                <img
                    style="opacity:{_opacity};position: absolute;height:{IrScalingHeight}px;left:0px;top:0px"
                    src="data:image/png;base64,{_imageCropPreview.visImage}"
                    alt="Vis Crop" />
            </div>
            <div class="col-md-4"></div>
            <div class="col-md-2 d-flex flex-column colm-3" style="height: 40vh;overflow-y:auto">
                {#each uniqueExtractionOffsets() as template}
                    <button on:click={() => UpdateFineAdjustement(true, template.x, template.y)} class="btn btn-dark"
                        >Try Offset (X,Y): {template.x} {template.y}</button>
                {/each}
            </div>
        </div>
    {/if}
</div>
