<script lang="ts">
    import {CvInterop} from "../deviceConfiguration/CvInterop";
    import ImageReplayer from "./ImageReplayer.svelte";
    import type {ReplayedImage} from "./ReplayedImage";
    let getLeftSelectedImage: () => ReplayedImage | undefined;
    let getRightSelectedImage: () => ReplayedImage | undefined;
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
        cvInterop.calculateImageOffset(leftImage.imageUrl, rightImage.imageUrl);
    }
</script>

<div class="col-md-12 row">
    <ImageReplayer bind:getSelectedImage={getLeftSelectedImage} class="col-md-6"></ImageReplayer>
    <ImageReplayer bind:getSelectedImage={getRightSelectedImage} class="col-md-6"></ImageReplayer>
</div>

<button on:click={AlignImages} class="btn btn-primary">Align images</button>
