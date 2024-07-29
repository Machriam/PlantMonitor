<script lang="ts">
    import {onDestroy, onMount} from "svelte";
    import {DeviceStreaming} from "~/services/DeviceStreaming";
    import {CvInterop} from "../deviceConfiguration/CvInterop";
    import {TooltipCreator, type TooltipCreatorResult} from "../reuseableComponents/TooltipCreator";
    import {cropImage, drawImageOnCanvas, resizeBase64Img} from "../replayPictures/ImageResizer";
    import type {ImageToCut} from "./ImageToCut";
    import {
        NpgsqlPoint,
        PhotoStitchingClient,
        PhotoTourPlantInfo,
        PictureTripData,
        PlantImageSection,
        type IIrCameraOffset
    } from "~/services/GatewayAppApi";
    import type {HubConnection} from "@microsoft/signalr";
    import {plantPolygonChanged, selectedDevice, selectedPhotoTourPlantInfo} from "../store";
    import type {Unsubscriber} from "svelte/motion";

    export let deviceId: string;
    export let irSeries: string;
    export let visSeries: string;
    export let _selectedPhotoTrip: PictureTripData;
    let _selectedImage: ImageToCut | undefined;
    let _currentImageIndex: number = -1;
    let _images: ImageToCut[] = [];
    let _lastPointerPosition: MouseEvent | undefined;
    let _tooltip: TooltipCreatorResult | undefined;
    let _cutPolygon: {point: NpgsqlPoint}[] = [];
    let _visConnection: HubConnection | undefined;
    let _irConnection: HubConnection | undefined;
    let _selectedPlant: PhotoTourPlantInfo | undefined;
    let _imageRatio: number = 0;
    let _polygonValid = false;
    let _baseOffset: IIrCameraOffset = {left: 0, top: 0};
    const _selectedThumbnailId = Math.random().toString(36);
    const _selectedImageCanvasId = Math.random().toString(36);
    const _cvInterop = new CvInterop();
    let _unsubscribe: Unsubscriber[] = [];
    onMount(() => {
        startStream();
        _baseOffset = $selectedDevice?.health.cameraOffset ?? {left: 0, top: 0};
        const unsubscriber = selectedPhotoTourPlantInfo.subscribe(async (x) => {
            _cutPolygon = [];
            await changeImage(_currentImageIndex);
            if (x == undefined) return;
            if (x.length == 1) _selectedPlant = x[0];
            else _selectedPlant = undefined;
            const stepCount = _selectedImage?.stepCount;
            const tripTime = _selectedPhotoTrip.timeStamp;
            for (let i = 0; i < x.length; i++) {
                const plant = x[i];
                const existingTemplate = plant.extractionTemplate
                    .filter((et) => et.motorPosition == stepCount && et.applicablePhotoTripFrom <= tripTime)
                    .toSorted((a, b) => b.applicablePhotoTripFrom.getTime() - a.applicablePhotoTripFrom.getTime())
                    .at(0);
                if (existingTemplate == undefined) continue;
                _cutPolygon = [];
                existingTemplate.photoBoundingBox.forEach((bb) => {
                    _cutPolygon.push({point: new NpgsqlPoint({x: bb.x * _imageRatio, y: bb.y * _imageRatio})});
                    drawLine();
                });
            }
        });
        _unsubscribe.push(unsubscriber);
    });
    onDestroy(() => {
        _irConnection?.stop();
        _visConnection?.stop();
        _irConnection = undefined;
        _visConnection = undefined;
        _unsubscribe.map((x) => x());
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
                const convertedImage = _cvInterop.thermalDataToImage(new Uint32Array(await image.arrayBuffer()));
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
        _cutPolygon = [];
        event.preventDefault();
    }
    function addLine(event: MouseEvent) {
        if (_selectedPlant == undefined) return;
        _cutPolygon.push({point: new NpgsqlPoint({x: event.offsetX, y: event.offsetY})});
        drawLine();
    }
    function drawLine() {
        if (_cutPolygon.length <= 1) return;
        const image = document.getElementById(_selectedImageCanvasId) as HTMLCanvasElement;
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
        _polygonValid = isPolygonValid();
    }
    async function connectPolygon() {
        if (_cutPolygon.length <= 2 || _selectedImage == null || _selectedPlant == undefined) return;
        _cutPolygon.push(_cutPolygon[0]);
        drawLine();
        const croppedImage = await cropImage(
            _selectedImage.imageUrl,
            _cutPolygon.map((p) => ({x: p.point.x / _imageRatio, y: p.point.y / _imageRatio}))
        );
        const qrCode = _cvInterop.readQRCode(croppedImage);
        console.log(qrCode);
        _cutPolygon = _cutPolygon;
        _polygonValid = true;
    }
    async function changeImage(newIndex: number) {
        _currentImageIndex = newIndex;
        _selectedImage = _images[_currentImageIndex];
        if (_selectedImage.imageUrl == undefined) return;
        const canvas = document.getElementById(_selectedImageCanvasId) as HTMLCanvasElement;
        const ratio = await drawImageOnCanvas(_selectedImage.imageUrl, canvas);
        _imageRatio = ratio.ratio;
        const activatedTooltip = document.getElementById(_selectedThumbnailId + "_" + _currentImageIndex);
        activatedTooltip?.scrollIntoView({behavior: "instant", block: "nearest", inline: "center"});
        updateTooltip();
    }
    function isPolygonValid() {
        return (
            _cutPolygon.length > 2 &&
            _cutPolygon[_cutPolygon.length - 1].point.x == _cutPolygon[0].point.x &&
            _cutPolygon[_cutPolygon.length - 1].point.y == _cutPolygon[0].point.y
        );
    }
    async function savePolygon() {
        if (_selectedImage == undefined || _selectedPlant == undefined || !_polygonValid) return;
        const client = new PhotoStitchingClient();
        await client.associatePlantImageSection(
            new PlantImageSection({
                plantId: _selectedPlant.id,
                irPolygonOffset: new NpgsqlPoint({x: _baseOffset.left ?? 0, y: _baseOffset.top ?? 0}),
                stepCount: _selectedImage.stepCount,
                photoTripId: _selectedPhotoTrip.tripId,
                polygon: _cutPolygon.map((p) => new NpgsqlPoint({x: p.point.x / _imageRatio, y: p.point.y / _imageRatio}))
            })
        );
        $plantPolygonChanged = _selectedPlant;
    }
    async function removePolygon() {
        if (_selectedPlant == undefined || _selectedImage == undefined) return;
        const client = new PhotoStitchingClient();
        const template = _selectedPlant.extractionTemplate.find((et) => et.motorPosition == _selectedImage!.stepCount);
        _cutPolygon = [];
        await changeImage(_currentImageIndex);
        if (template == undefined) return;
        await client.removePlantImageSections([template.id]);
        $plantPolygonChanged = _selectedPlant;
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
            id={_selectedImageCanvasId}
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
                    <div id={_selectedThumbnailId + "_" + i} style="height: 80px;width:70px">
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
        {#if _selectedPlant != undefined}
            {#if _cutPolygon.length > 2}
                <button on:click={() => connectPolygon()} class="btn btn-primary">Connect Cut</button>
            {/if}
        {/if}
        <button on:click={removePolygon} class="btn btn-danger">Delete Polygon</button>
        <button on:click={savePolygon} disabled={!_polygonValid} class="ms-2 btn btn-success">Save Polygon</button>
    </div>
</div>
