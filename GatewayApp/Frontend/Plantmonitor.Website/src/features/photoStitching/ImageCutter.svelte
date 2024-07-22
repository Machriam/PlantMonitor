<script lang="ts">
    import {onMount} from "svelte";
    import {DeviceStreaming} from "~/services/DeviceStreaming";
    import {CvInterop} from "../deviceConfiguration/CvInterop";
    import {TooltipCreator, type TooltipCreatorResult} from "../reuseableComponents/TooltipCreator";
    import {resizeBase64Img} from "../replayPictures/ImageResizer";
    import type {ImageToCut} from "./ImageToCut";

    export let deviceId: string;
    export let irSeries: string;
    export let visSeries: string;
    let selectedImage: ImageToCut | undefined;
    let currentImageIndex: number = -1;
    let images: ImageToCut[] = [];
    let lastPointerPosition: MouseEvent | undefined;
    let tooltip: TooltipCreatorResult | undefined;
    const selectedImageDivId = Math.random().toString(36);
    const cvInterop = new CvInterop();
    onMount(() => {
        startStream();
    });
    function startStream() {
        const streamer = new DeviceStreaming();
        const visConnection = streamer.replayPictures(deviceId, visSeries);
        const irConnection = streamer.replayPictures(deviceId, irSeries);
        images = [];
        currentImageIndex = -1;
        visConnection.start(async (step, date, image, temperature) => {
            console.log(step);
            let dataUrl = "";
            let pixelConverter = undefined;
            dataUrl = await image.asBase64Url();
            const thumbnail = await resizeBase64Img(dataUrl, 100, 100);
            images.push({
                imageUrl: dataUrl,
                stepCount: step,
                date: date,
                irDataUrl: "",
                temperature: temperature,
                thumbnailUrl: thumbnail,
                pixelConverter: pixelConverter
            });
            if (images.length == 1) {
                currentImageIndex = 0;
                selectedImage = images[currentImageIndex];
            }
            images = images;
        });
        visConnection.connection.onclose(() => {
            irConnection.start(async (step, date, image, temperature) => {
                let dataUrl = "";
                let pixelConverter = undefined;
                debugger;
                const convertedImage = cvInterop.thermalDataToImage(new Uint32Array(await image.arrayBuffer()));
                pixelConverter = convertedImage.pixelConverter;
                dataUrl = convertedImage.dataUrl ?? "";
                const visImage = images.find((i) => i.stepCount == step);
                if (visImage !== undefined) {
                    visImage.irDataUrl = dataUrl;
                    visImage.temperature = temperature;
                }
            });
        });
    }
    function onScroll(event: WheelEvent) {
        if (currentImageIndex == -1) return;
        let currentIndex = currentImageIndex;
        if (event.deltaY < 0 && currentIndex > 0) {
            currentIndex = currentIndex - 1;
        } else if (event.deltaY > 0 && currentIndex < images.length - 1) {
            currentIndex = currentIndex + 1;
        }
        changeImage(currentIndex);
        event.preventDefault();
    }
    function changeImage(newIndex: number) {
        currentImageIndex = newIndex;
        selectedImage = images[currentImageIndex];
        const activatedTooltip = document.getElementById(selectedImageDivId + "_" + currentImageIndex);
        activatedTooltip?.scrollIntoView({behavior: "instant", block: "nearest", inline: "center"});
        updateTooltip();
    }
    function updateTooltip() {
        if (selectedImage?.pixelConverter == null || tooltip == undefined || lastPointerPosition == null) return;
        const value = selectedImage.pixelConverter(lastPointerPosition.offsetX, lastPointerPosition.offsetY);
        tooltip.updateFunction(lastPointerPosition, value.toFixed(2) + " °C");
    }
</script>

<div class={$$restProps.class || ""}>
    <div class="col-md-12 row p-0" style="min-height: 120px;" on:wheel={(x) => onScroll(x)}>
        {#if selectedImage != undefined}
            <img
                on:mouseenter={(x) => {
                    if (tooltip != undefined || selectedImage?.pixelConverter == null) return;
                    lastPointerPosition = x;
                    tooltip = TooltipCreator.CreateTooltip("", x);
                    updateTooltip();
                }}
                on:mouseleave={() => {
                    if (tooltip == undefined) return;
                    tooltip.dispose();
                    tooltip = undefined;
                }}
                on:pointermove={(x) => {
                    lastPointerPosition = x;
                    updateTooltip();
                }}
                alt="preview"
                style="width:{selectedImage.pixelConverter != undefined ? 'initial' : ''}"
                src={selectedImage?.imageUrl} />
            <div class="col-md-3">
                <div>{selectedImage.date.toLocaleTimeString()}</div>
                <div>Position: {selectedImage.stepCount}</div>
                <div>Image {currentImageIndex + 1}</div>
                {#if selectedImage.temperature != undefined && selectedImage.temperature > 0}
                    <div>Temperature: {selectedImage.temperature}</div>
                {/if}
            </div>
            <div style="overflow-x:auto;width:40vw;flex-flow:nowrap;min-height:120px" class="row p-0">
                {#each images as image, i}
                    <div id={selectedImageDivId + "_" + i} style="height: 80px;width:70px">
                        <button class="p-0 m-0" on:click={() => changeImage(i)} style="height: 70px;width:70px;border:unset">
                            <img style="height: 100%;width:100%" alt="visual scrollbar" src={image.thumbnailUrl} />
                        </button>
                        <div style="font-weight: {i == currentImageIndex ? '700' : '400'};">{i + 1}</div>
                    </div>
                {/each}
            </div>
        {/if}
    </div>
</div>
