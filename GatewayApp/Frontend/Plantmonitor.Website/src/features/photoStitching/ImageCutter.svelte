<script lang="ts">
    import {onDestroy, onMount} from "svelte";
    import {DeviceStreaming} from "~/services/DeviceStreaming";
    import {CvInterop, IrScalingHeight, IrScalingWidth} from "../deviceConfiguration/CvInterop";
    import {TooltipCreator, type TooltipCreatorResult} from "../reuseableComponents/TooltipCreator";
    import {drawImageOnCanvas, resizeBase64Img} from "../replayPictures/ImageResizer";
    import type {ImageToCut} from "./ImageToCut";
    import {
        NpgsqlPoint,
        PhotoStitchingClient,
        PhotoTourPlantInfo,
        PictureTripData,
        PlantExtractionTemplateModel,
        PlantImageSection
    } from "~/services/GatewayAppApi";
    import type {HubConnection} from "@microsoft/signalr";
    import {imageToCutChanged, plantPolygonChanged, selectedPhotoTourPlantInfo} from "./PhotoStitchingContext";
    import type {Unsubscriber} from "svelte/motion";
    import {selectedDevice} from "../store";
    import {Task} from "~/types/Task";
    import {pipe} from "~/types/Pipe";

    export let deviceId: string;
    export let irSeries: string;
    export let visSeries: string;
    export let _selectedPhotoTrip: PictureTripData;
    export let _extractionTemplates: PlantExtractionTemplateModel[];
    let _selectedImage: ImageToCut | undefined;
    let _currentImageIndex: number = -1;
    let _images: ImageToCut[] = [];
    let _lastPointerPosition: MouseEvent | undefined;
    let _tooltip: TooltipCreatorResult | undefined;
    let _cutPolygon: {points: NpgsqlPoint[]; name: string; position: string} = {points: [], name: "", position: ""};
    let _visConnection: HubConnection | undefined;
    let _irConnection: HubConnection | undefined;
    let _selectedPlant: PhotoTourPlantInfo | undefined;
    let _imageRatio: number = 0;
    let _polygonValid = false;
    const _selectedThumbnailId = Math.random().toString(36);
    const _selectedImageCanvasId = Math.random().toString(36);
    const _cvInterop = new CvInterop();
    let _unsubscribe: Unsubscriber[] = [];
    onMount(() => {
        startStream();
        const unsubscriber = selectedPhotoTourPlantInfo.subscribe(async (x) => {
            _cutPolygon = {points: [], name: "", position: ""};
            await refreshImage();
            if (x == undefined) return;
            if (x.length == 1) _selectedPlant = x[0];
            else _selectedPlant = undefined;
            for (let i = 0; i < x.length; i++) {
                const plant = x[i];
                const existingTemplate = _extractionTemplates.find(
                    (et) => et.photoTourPlantFk == plant.id && et.motorPosition == _selectedImage?.stepCount
                );
                if (existingTemplate == undefined) continue;
                _cutPolygon = {points: [], name: plant.name, position: plant.position ?? ""};
                existingTemplate.photoBoundingBox.forEach((bb) => {
                    _cutPolygon.points.push(new NpgsqlPoint({x: bb.x * _imageRatio, y: bb.y * _imageRatio}));
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
            dataUrl = (await pipe(image).asBase64Url()).valueOf();
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
        _cutPolygon = {points: [], name: "", position: ""};
        event.preventDefault();
    }
    async function addLine(event: MouseEvent) {
        if (_selectedPlant == undefined) return;
        if (isPolygonValid()) {
            _cutPolygon = {points: [], name: _selectedPlant.name, position: _selectedPlant.position ?? ""};
            await refreshImage();
        }
        _cutPolygon.points.push(new NpgsqlPoint({x: event.offsetX, y: event.offsetY}));
        drawLine();
    }
    function drawLine() {
        if (_cutPolygon.points.length <= 1) return;
        const image = document.getElementById(_selectedImageCanvasId) as HTMLCanvasElement;
        const context = image.getContext("2d");
        if (context == null) return;
        context.beginPath();
        const startPoint = _cutPolygon.points[_cutPolygon.points.length - 2];
        const endPoint = _cutPolygon.points[_cutPolygon.points.length - 1];
        context.moveTo(startPoint.x, startPoint.y);
        context.lineTo(endPoint.x, endPoint.y);
        context.lineWidth = 3;
        context.strokeStyle = "yellow";
        context.stroke();
        _polygonValid = isPolygonValid();
        if (_polygonValid) {
            const sortedXValues = _cutPolygon.points.map((p) => p.x).toSorted((a, b) => a - b);
            const sortedYValues = _cutPolygon.points.map((p) => p.y).toSorted((a, b) => a - b);
            var midY = sortedYValues[0] + (sortedYValues[sortedYValues.length - 1] - sortedYValues[0]) / 2;
            var midX = sortedXValues[0] + (sortedXValues[sortedXValues.length - 1] - sortedXValues[0]) / 2;
            context.font = "20px Arial";
            context.lineWidth = 1;
            context.textAlign = "center";
            const textMeasurement = context.measureText(_cutPolygon.position);
            const height = textMeasurement.fontBoundingBoxAscent + textMeasurement.fontBoundingBoxDescent;
            if (!pipe(_cutPolygon.position).isEmpty()) context.strokeText(_cutPolygon.position, midX, midY - height);
            context.strokeText(_cutPolygon.name, midX, midY);
        }
    }
    async function connectPolygon() {
        if (_cutPolygon.points.length <= 2 || _selectedImage == null || _selectedPlant == undefined) return;
        _cutPolygon.points.push(_cutPolygon.points[0]);
        drawLine();
        _cutPolygon = _cutPolygon;
        _polygonValid = true;
    }
    async function drawIrBorder() {
        const offset = $selectedDevice?.health.cameraOffset ?? {left: 0, top: 0};
        const image = document.getElementById(_selectedImageCanvasId) as HTMLCanvasElement;
        const context = image.getContext("2d");
        if (context == null) return;
        const ratio = context.canvas.height / IrScalingHeight;
        const irLeft = (offset.left ?? 0) * ratio;
        const irTop = (offset.top ?? 0) * ratio;
        const irRight = Math.min(((offset.left ?? 0) + IrScalingWidth) * ratio, context.canvas.width);
        const irBottom = Math.min(((offset.top ?? 0) + IrScalingHeight) * ratio, context.canvas.height);
        context.beginPath();
        const points = [
            new NpgsqlPoint({x: irLeft, y: irTop}),
            new NpgsqlPoint({x: irRight, y: irTop}),
            new NpgsqlPoint({x: irRight, y: irBottom}),
            new NpgsqlPoint({x: irLeft, y: irBottom}),
            new NpgsqlPoint({x: irLeft, y: irTop})
        ];
        for (let i = 0; i < points.length - 1; i++) {
            const startPoint = points[i];
            const endPoint = points[i + 1];
            context.moveTo(startPoint.x, startPoint.y);
            context.lineTo(endPoint.x, endPoint.y);
            context.lineWidth = 1;
            context.strokeStyle = "blue";
            context.stroke();
        }
    }
    async function refreshImage() {
        if (_selectedImage?.imageUrl == undefined) return;
        const canvas = document.getElementById(_selectedImageCanvasId) as HTMLCanvasElement;
        await Task.delay(500);
        const ratio = await drawImageOnCanvas(_selectedImage.imageUrl, canvas);
        _imageRatio = ratio.ratio;
        const activatedTooltip = document.getElementById(_selectedThumbnailId + "_" + _currentImageIndex);
        activatedTooltip?.scrollIntoView({behavior: "instant", block: "nearest", inline: "center"});
        await drawIrBorder();
        updateTooltip();
    }
    async function changeImage(newIndex: number) {
        _currentImageIndex = newIndex;
        _selectedImage = _images[_currentImageIndex];
        $imageToCutChanged = _selectedImage;
        await refreshImage();
    }
    function isPolygonValid() {
        return (
            _cutPolygon.points.length > 2 &&
            _cutPolygon.points[_cutPolygon.points.length - 1].x == _cutPolygon.points[0].x &&
            _cutPolygon.points[_cutPolygon.points.length - 1].y == _cutPolygon.points[0].y
        );
    }
    async function savePolygon() {
        if (_selectedImage == undefined || _selectedPlant == undefined || !_polygonValid) return;
        const client = new PhotoStitchingClient();
        await client.associatePlantImageSection(
            new PlantImageSection({
                plantId: _selectedPlant.id,
                irPolygonOffset: new NpgsqlPoint({
                    x: $selectedDevice?.health.cameraOffset?.left ?? 0,
                    y: $selectedDevice?.health.cameraOffset?.top ?? 0
                }),
                stepCount: _selectedImage.stepCount,
                photoTripId: _selectedPhotoTrip.tripId,
                polygon: _cutPolygon.points.map((p) => new NpgsqlPoint({x: p.x / _imageRatio, y: p.y / _imageRatio}))
            })
        );
        $plantPolygonChanged = _selectedPlant;
    }
    async function removePolygon() {
        if (_selectedPlant == undefined || _selectedImage == undefined) return;
        const client = new PhotoStitchingClient();
        const template = _extractionTemplates.find((et) => et.photoTourPlantFk == _selectedPlant?.id);
        _cutPolygon = {points: [], name: "", position: ""};
        await refreshImage();
        if (template == undefined) return;
        if (template.photoTripFk != _selectedPhotoTrip.tripId) {
            alert("Polygon must be deleted from trip: " + template.applicablePhotoTripFrom.toLocaleString());
            return;
        }
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
            {#if _cutPolygon.points.length > 2}
                <button on:click={() => connectPolygon()} class="btn btn-primary">Connect Cut</button>
            {/if}
        {/if}
        <button on:click={removePolygon} class="btn btn-danger ms-2">Delete Polygon</button>
        <button on:click={savePolygon} disabled={!_polygonValid} class="ms-2 btn btn-success">Save Polygon</button>
    </div>
</div>
