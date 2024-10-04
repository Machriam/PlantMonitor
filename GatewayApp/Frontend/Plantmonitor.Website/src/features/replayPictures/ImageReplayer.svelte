<script lang="ts">
    import {onDestroy, onMount} from "svelte";
    import {CameraType, PictureClient, PictureSeriesData, SeriesByDevice} from "~/services/GatewayAppApi";
    import {selectedDevice} from "../store";
    import {DeviceStreaming} from "~/services/DeviceStreaming";
    import type {HubConnection} from "@microsoft/signalr";
    import {CvInterop} from "../deviceConfiguration/CvInterop";
    import {TooltipCreator, type TooltipCreatorResult} from "../reuseableComponents/TooltipCreator";
    import Select from "../reuseableComponents/Select.svelte";
    import {resizeBase64Img} from "./ImageResizer";
    import type {ReplayedImage} from "./ReplayedImage";
    import {pipe} from "~/types/Pipe";
    import DateInput from "../reuseableComponents/DateInput.svelte";

    let _pictureSeries: PictureSeriesData[] = [];
    export const getSelectedImage = () => _selectedImage;
    let _selectedSeries: PictureSeriesData | undefined;
    let _hubConnection: HubConnection | undefined;
    let _selectedImage: ReplayedImage | undefined;
    let _currentImageIndex: number = -1;
    let _images: ReplayedImage[] = [];
    let _lastPointerPosition: MouseEvent | undefined;
    let _tooltip: TooltipCreatorResult | undefined;
    let _seriesByDevice: SeriesByDevice[] = [];
    let _selectedDeviceId: string | undefined;
    let _selectedDate: Date = new Date();
    const _selectedImageDivId = Math.random().toString(36);
    const _cvInterop = new CvInterop();
    onMount(async () => {
        await updatePictureSeries($selectedDevice?.health.deviceId);
    });
    const subscription = selectedDevice.subscribe(async (x) => {
        await updatePictureSeries($selectedDevice?.health.deviceId);
    });
    async function updatePictureSeries(deviceId: string | undefined) {
        const pictureClient = new PictureClient();
        _seriesByDevice = await pictureClient.getAllPicturedDevices();
        _selectedDeviceId = deviceId;
        if (deviceId == undefined) {
            _pictureSeries = [];
            return;
        }
        _pictureSeries = await pictureClient.getPictureSeries(deviceId, _selectedDate);
    }
    async function onDateChanged(date: Date) {
        if (_selectedDate == date || _selectedDeviceId == undefined) return;
        _selectedDate = date;
        const pictureClient = new PictureClient();
        _pictureSeries = await pictureClient.getPictureSeries(_selectedDeviceId, _selectedDate);
    }
    function onSeriesSelected(series: PictureSeriesData) {
        if (_selectedDeviceId == undefined) return;
        _selectedSeries = series;
        const streamer = new DeviceStreaming();
        const connection = streamer.replayPictures(_selectedDeviceId, series.folderName);
        _hubConnection?.stop();
        _hubConnection = connection.connection;
        _images = [];
        _currentImageIndex = -1;
        connection.start(async (step, date, image, temperature) => {
            let dataUrl = "";
            let pixelConverter = undefined;
            if (series.type == CameraType.IR) {
                const convertedImage = _cvInterop.thermalDataToImage(new Uint32Array(await image.arrayBuffer()));
                pixelConverter = convertedImage.pixelConverter;
                dataUrl = convertedImage.dataUrl ?? "";
            } else {
                dataUrl = (await pipe(image).asBase64Url()).valueOf();
            }
            const thumbnail = await resizeBase64Img(dataUrl, 100, 100);
            _images.push({
                imageUrl: dataUrl,
                stepCount: step,
                date: date,
                temperature: temperature,
                thumbnailUrl: thumbnail,
                pixelConverter: pixelConverter
            });
            if (_images.length == 1) {
                _currentImageIndex = 0;
                _selectedImage = _images[_currentImageIndex];
            }
            _images = _images;
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
    function changeImage(newIndex: number) {
        _currentImageIndex = newIndex;
        _selectedImage = _images[_currentImageIndex];
        const activatedTooltip = document.getElementById(_selectedImageDivId + "_" + _currentImageIndex);
        activatedTooltip?.scrollIntoView({behavior: "instant", block: "nearest", inline: "center"});
        updateTooltip();
    }
    function updateTooltip() {
        if (_selectedImage?.pixelConverter == null || _tooltip == undefined || _lastPointerPosition == null) return;
        const value = _selectedImage.pixelConverter(_lastPointerPosition.offsetX, _lastPointerPosition.offsetY);
        _tooltip.updateFunction(_lastPointerPosition, value.toFixed(2) + " °C");
    }
    onDestroy(() => {
        subscription();
        _hubConnection?.stop();
    });
</script>

<div class={$$restProps.class || ""}>
    <div class="col-md-12 row">
        <Select
            initialSelectedItem={$selectedDevice?.health.deviceId}
            idSelector={(x) => x.deviceId}
            textSelector={(x) => x.deviceId}
            selectedItemChanged={(x) => updatePictureSeries(x?.deviceId)}
            items={_seriesByDevice}
            class="col-md-6"></Select>
        <DateInput class="col-md-4" label="From" valueHasChanged={onDateChanged}></DateInput>
    </div>
    <div style="height: 10vh;overflow-y:auto;text-align-last:left" class="d-flex flex-column col-md-12">
        {#each _pictureSeries as series}
            <button
                on:click={() => onSeriesSelected(series)}
                style="border-width: 1px;border-style: solid;border-color:grey"
                class="col-md-12 row alert border-0 m-0 p-0 {_selectedSeries == series ? 'alert-info' : ''}">
                <span class="col-md-2">{series.count}</span>
                <span class="col-md-1">{series.type == CameraType.IR ? "IR" : "Vis"}</span>
                <span class="col-md-9">{series.timestamp.toLocaleString()}</span>
            </button>
        {/each}
    </div>
    <div class="col-md-12 row p-0" style="min-height: 120px;" on:wheel={(x) => onScroll(x)}>
        {#if _selectedImage != undefined}
            <img
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
                alt="preview"
                style="width:{_selectedImage.pixelConverter != undefined ? 'initial' : ''}"
                src={_selectedImage?.imageUrl} />
            <div class="col-md-3">
                <div>{_selectedImage.date.toLocaleTimeString()}</div>
                <div>Position: {_selectedImage.stepCount}</div>
                <div>Image {_currentImageIndex + 1}/{_selectedSeries?.count}</div>
                {#if _selectedImage.temperature != undefined && _selectedImage.temperature > 0}
                    <div>Temperature: {_selectedImage.temperature}</div>
                {/if}
            </div>
            <div style="overflow-x:auto;width:40vw;flex-flow:nowrap;min-height:120px" class="row p-0">
                {#each _images as image, i}
                    <div id={_selectedImageDivId + "_" + i} style="height: 80px;width:70px">
                        <button class="p-0 m-0" on:click={() => changeImage(i)} style="height: 70px;width:70px;border:unset">
                            <img style="height: 100%;width:100%" alt="visual scrollbar" src={image.thumbnailUrl} />
                        </button>
                        <div style="font-weight: {i == _currentImageIndex ? '700' : '400'};">{i + 1}</div>
                    </div>
                {/each}
            </div>
        {/if}
    </div>
</div>
