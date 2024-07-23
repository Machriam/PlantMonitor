<script lang="ts">
    import {onDestroy, onMount} from "svelte";
    import {DeviceStreaming} from "~/services/DeviceStreaming";
    import {CvInterop} from "../deviceConfiguration/CvInterop";
    import {TooltipCreator, type TooltipCreatorResult} from "../reuseableComponents/TooltipCreator";
    import {drawImageOnCanvas, resizeBase64Img} from "../replayPictures/ImageResizer";
    import type {ImageToCut} from "./ImageToCut";
    import {NpgsqlPoint} from "~/services/GatewayAppApi";
    import {Task} from "~/types/task";
    import TextInput from "../reuseableComponents/TextInput.svelte";
    import type {HubConnection} from "@microsoft/signalr";

    export let deviceId: string;
    export let irSeries: string;
    export let visSeries: string;
    let _selectedImage: ImageToCut | undefined;
    let _currentImageIndex: number = -1;
    let _images: ImageToCut[] = [];
    let _lastPointerPosition: MouseEvent | undefined;
    let _tooltip: TooltipCreatorResult | undefined;
    let _cutPolygon: {point: NpgsqlPoint; rendered: boolean}[] = [];
    let _addPolygonOn = {activated: false, name: ""};
    let _visConnection: HubConnection | undefined;
    let _irConnection: HubConnection | undefined;
    const selectedThumbnailId = Math.random().toString(36);
    const selectedImageCanvasId = Math.random().toString(36);
    const cvInterop = new CvInterop();
    onMount(() => {
        startStream();
    });
    onDestroy(() => {
        _irConnection?.stop();
        _visConnection?.stop();
        _irConnection = undefined;
        _visConnection = undefined;
    });
    function startStream() {
        const streamer = new DeviceStreaming();
        const visConnection = streamer.replayPictures(deviceId, visSeries);
        const irConnection = streamer.replayPictures(deviceId, irSeries);
        _visConnection = visConnection.connection;
        _irConnection = irConnection.connection;

        _images = [];
        _currentImageIndex = -1;
        visConnection.start(async (step, date, image, temperature) => {
            let dataUrl = "";
            let pixelConverter = undefined;
            dataUrl = await image.asBase64Url();
            const thumbnail = await resizeBase64Img(dataUrl, 100, 100);
            _images.push({
                imageUrl: dataUrl,
                stepCount: step,
                date: date,
                irDataUrl: "",
                temperature: temperature,
                thumbnailUrl: thumbnail,
                pixelConverter: pixelConverter
            });
            if (_images.length == 1) {
                await changeImage(0);
            }
            _images = _images;
        });
        visConnection.connection.onclose(() => {
            irConnection.start(async (step, date, image, temperature) => {
                let dataUrl = "";
                let pixelConverter = undefined;
                const convertedImage = cvInterop.thermalDataToImage(new Uint32Array(await image.arrayBuffer()));
                pixelConverter = convertedImage.pixelConverter;
                dataUrl = convertedImage.dataUrl ?? "";
                const visImage = _images.find((i) => i.stepCount == step);
                if (visImage !== undefined) {
                    visImage.irDataUrl = dataUrl;
                    visImage.temperature = temperature;
                }
            });
        });
    }
    function onScroll(event: WheelEvent) {
        if (_currentImageIndex == -1) return;
        let currentIndex = _currentImageIndex;
        if (event.deltaY < 0 && currentIndex > 0) {
            currentIndex = currentIndex - 1;
        } else if (event.deltaY > 0 && currentIndex < _images.length - 1) {
            currentIndex = currentIndex + 1;
        }
        changeImage(currentIndex);
        event.preventDefault();
    }
    function addLine(event: MouseEvent) {
        if (!_addPolygonOn.activated) return;
        _cutPolygon.push({point: new NpgsqlPoint({x: event.offsetX, y: event.offsetY}), rendered: false});
        drawLine();
    }
    function drawLine() {
        if (_cutPolygon.length == 1) return;
        const image = document.getElementById(selectedImageCanvasId) as HTMLCanvasElement;
        const context = image.getContext("2d");
        if (context == null) return;
        context.beginPath();
        const startPoint = _cutPolygon[_cutPolygon.length - 2];
        const endPoint = _cutPolygon[_cutPolygon.length - 1];
        context.moveTo(startPoint.point.x, startPoint.point.y);
        context.lineTo(endPoint.point.x, endPoint.point.y);
        context.lineWidth = 3;
        context.strokeStyle = "yellow";
        context.stroke();
    }
    function connectPolygon() {
        if (!_addPolygonOn.activated || _cutPolygon.length <= 2) return;
        _cutPolygon.push(_cutPolygon[0]);
        drawLine();
        _cutPolygon = _cutPolygon;
    }
    async function changeImage(newIndex: number) {
        _currentImageIndex = newIndex;
        _selectedImage = _images[_currentImageIndex];
        const canvas = document.getElementById(selectedImageCanvasId) as HTMLCanvasElement;
        await drawImageOnCanvas(_selectedImage.imageUrl, canvas);
        const activatedTooltip = document.getElementById(selectedThumbnailId + "_" + _currentImageIndex);
        activatedTooltip?.scrollIntoView({behavior: "instant", block: "nearest", inline: "center"});
        updateTooltip();
    }
    function updateTooltip() {
        if (_selectedImage?.pixelConverter == null || _tooltip == undefined || _lastPointerPosition == null) return;
        const value = _selectedImage.pixelConverter(_lastPointerPosition.offsetX, _lastPointerPosition.offsetY);
        _tooltip.updateFunction(_lastPointerPosition, value.toFixed(2) + " °C");
    }
</script>

<div class={$$restProps.class || ""}>
    <div class="col-md-12 row p-0" style="min-height: 120px;" on:wheel={(x) => onScroll(x)}>
        <canvas
            on:mouseenter={(x) => {
                if (_tooltip != undefined || _selectedImage?.pixelConverter == null) return;
                _lastPointerPosition = x;
                _tooltip = TooltipCreator.CreateTooltip("", x);
                updateTooltip();
            }}
            on:mouseleave={() => {
                if (_tooltip == undefined) return;
                _tooltip.dispose();
                _tooltip = undefined;
            }}
            on:pointermove={(x) => {
                _lastPointerPosition = x;
                updateTooltip();
            }}
            on:mousedown={(x) => addLine(x)}
            id={selectedImageCanvasId}
            style="width:{_selectedImage?.pixelConverter != undefined ? 'initial' : ''}">
        </canvas>
        {#if _selectedImage != undefined}
            <div class="col-md-3">
                <div>{_selectedImage.date.toLocaleTimeString()}</div>
                <div>Position: {_selectedImage.stepCount}</div>
                <div>Image {_currentImageIndex + 1}</div>
                {#if _selectedImage.temperature != undefined && _selectedImage.temperature > 0}
                    <div>Temperature: {_selectedImage.temperature}</div>
                {/if}
            </div>
            <div style="overflow-x:auto;width:40vw;flex-flow:nowrap;min-height:120px" class="row p-0">
                {#each _images as image, i}
                    <div id={selectedThumbnailId + "_" + i} style="height: 80px;width:70px">
                        <button class="p-0 m-0" on:click={() => changeImage(i)} style="height: 70px;width:70px;border:unset">
                            <img style="height: 100%;width:100%" alt="visual scrollbar" src={image.thumbnailUrl} />
                        </button>
                        <div style="font-weight: {i == _currentImageIndex ? '700' : '400'};">{i + 1}</div>
                    </div>
                {/each}
            </div>
        {/if}
    </div>
    <div class="d-flex flex-row">
        <button
            on:click={() => (_addPolygonOn = {activated: !_addPolygonOn.activated, name: _addPolygonOn.name})}
            class="btn btn-primary">{_addPolygonOn.activated ? "Add Plant" : "Cut Plant"}</button>
        {#if _addPolygonOn.activated}
            {#if _cutPolygon.length > 2}
                <button on:click={() => connectPolygon()} class="btn btn-primary">Connect Cut</button>
            {/if}
            <TextInput class="col-md-3" label="Plant Name" bind:value={_addPolygonOn.name}></TextInput>
        {/if}
        <button class="btn btn-danger">Delete Cut</button>
    </div>
</div>
