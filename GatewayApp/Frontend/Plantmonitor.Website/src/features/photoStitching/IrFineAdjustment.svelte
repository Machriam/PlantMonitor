<script lang="ts">
    import {
        ImageCropPreview,
        PhotoStitchingClient,
        PlantExtractionTemplateModel,
        type PictureTripData
    } from "~/services/GatewayAppApi";
    import Checkbox from "../reuseableComponents/Checkbox.svelte";
    import {selectedPhotoTourPlantInfo} from "../store";
    let _isOpened = false;
    export let _selectedTrip: PictureTripData | undefined;
    export let _extractionTemplates: PlantExtractionTemplateModel[] = [];
    let _imageCropPreview: ImageCropPreview | undefined;
    async function UpdateCrop() {
        if (_extractionTemplates.length == 0 || _selectedTrip == undefined || $selectedPhotoTourPlantInfo == undefined) return;
        const extractionTemplateId = _extractionTemplates.find(
            (et) => et.photoTourPlantFk == $selectedPhotoTourPlantInfo[0].id
        )?.id;
        const client = new PhotoStitchingClient();
        _imageCropPreview = await client.croppedImageFor(extractionTemplateId, _selectedTrip.tripId);
    }
</script>

<div style="height:50vh" class="col-md-12 d-flex flex-column">
    <Checkbox label="Show IR fine adjustment" bind:value={_isOpened}></Checkbox>
    {#if _isOpened}
        {#if $selectedPhotoTourPlantInfo?.length != undefined && $selectedPhotoTourPlantInfo.length > 0}
            <div>
                Fine adjustement for {$selectedPhotoTourPlantInfo[0].name}
                {$selectedPhotoTourPlantInfo[0].comment} - {$selectedPhotoTourPlantInfo[0].qrCode}
            </div>
            <div>{_selectedTrip?.timeStamp}</div>
            <button on:click={UpdateCrop} class="btn btn-primary">Update Crop</button>
        {/if}
        {#if _imageCropPreview != undefined}
            <div style="height: 480px;" class="col-md-12 row">
                <img class="col-md-4" src="data:image/png;base64,{_imageCropPreview.irImage}" alt="IR Crop" />
                <img class="col-md-4" src="data:image/png;base64,{_imageCropPreview.visImage}" alt="Vis Crop" />
            </div>
        {/if}
    {/if}
</div>
