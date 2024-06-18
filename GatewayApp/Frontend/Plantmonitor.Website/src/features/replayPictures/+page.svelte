<script lang="ts">
    "@hmr:keep-all";
    import NumberInput from "../reuseableComponents/NumberInput.svelte";
    import {CvInterop, type ImageOffsetCalculator} from "../deviceConfiguration/CvInterop";
    import ImageReplayer from "./ImageReplayer.svelte";
    import type {ReplayedImage} from "./ReplayedImage";
    import {selectedDevice} from "../store";
    import {type IIrCameraOffset, PictureClient, IrCameraOffset} from "~/services/GatewayAppApi";
    let getLeftSelectedImage: () => ReplayedImage | undefined;
    let getRightSelectedImage: () => ReplayedImage | undefined;
    let imageOffsetCalculator: ImageOffsetCalculator | undefined;
    let leftOffset: number = 0;
    let topOffset: number = 0;
    let opacity: number = 0.5;
    $: (() => {
        imageOffsetCalculator?.leftControl(leftOffset);
    })();
    $: (() => {
        imageOffsetCalculator?.topControl(topOffset);
    })();
    $: (() => {
        imageOffsetCalculator?.visOpacity(opacity);
    })();
    async function StoreAlignment() {
        if ($selectedDevice?.ip.isEmpty()) return;
        const pictureClient = new PictureClient();
        const newOffset: IIrCameraOffset = {left: leftOffset, top: topOffset};
        pictureClient.updateIrOffset(new IrCameraOffset(newOffset), $selectedDevice?.ip);
    }
    async function AlignImages() {
        let leftImage = getLeftSelectedImage();
        let rightImage = getRightSelectedImage();
        if (rightImage?.pixelConverter != undefined) {
            const tempImage = leftImage;
            leftImage = rightImage;
            rightImage = tempImage;
        }
        if (leftImage == undefined || rightImage == undefined || leftImage.pixelConverter == undefined) return;
        const cvInterop = new CvInterop();
        if (imageOffsetCalculator != undefined) imageOffsetCalculator.delete();
        imageOffsetCalculator = cvInterop.calculateImageOffset(leftImage.imageUrl, rightImage.imageUrl);
        if ($selectedDevice != undefined) {
            leftOffset = $selectedDevice.health.cameraOffset?.left ?? 0;
            topOffset = $selectedDevice.health.cameraOffset?.top ?? 0;
        }
    }
</script>

<div class="col-md-12 row">
    <ImageReplayer bind:getSelectedImage={getLeftSelectedImage} class="col-md-6"></ImageReplayer>
    <ImageReplayer bind:getSelectedImage={getRightSelectedImage} class="col-md-6"></ImageReplayer>
</div>

<button on:click={AlignImages} class="btn btn-primary mt-2 mb-2">Align images</button>
{#if imageOffsetCalculator != undefined}
    <div class="row mb-2">
        <NumberInput class="col-md-2" bind:value={leftOffset} label="Left Offset"></NumberInput>
        <NumberInput class="col-md-2" bind:value={topOffset} label="Top Offset"></NumberInput>
        <NumberInput step={0.1} class="col-md-2" bind:value={opacity} label="Opacity"></NumberInput>
        <div class="col-md-4"></div>
        <button
            disabled={$selectedDevice == undefined || $selectedDevice.ip.isEmpty()}
            on:click={StoreAlignment}
            class="btn btn-primary col-md-2">Store Alignment</button>
    </div>
{/if}
