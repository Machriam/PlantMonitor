<script lang="ts">
    import {onDestroy, onMount} from "svelte";
    import {
        DashboardClient,
        DownloadInfo,
        PhotoTourInfo,
        SegmentationParameter,
        SegmentationTemplate,
        VirtualImageInfo
    } from "~/services/GatewayAppApi";
    import NumberInput from "~/features/reuseableComponents/NumberInput.svelte";
    import {Download} from "~/types/Download";
    import {_segmentationChanged, _selectedTourChanged, _virtualImageFilterByTime} from "./DashboardContext";
    import type {Unsubscriber} from "svelte/motion";
    import Checkbox from "../reuseableComponents/Checkbox.svelte";
    import {pipe} from "~/types/Pipe";
    import TextInput from "../reuseableComponents/TextInput.svelte";
    let _selectedTour: PhotoTourInfo | undefined | null;
    let _virtualImages: VirtualImageInfo[] = [];
    export let _selectedImage: VirtualImageInfo | undefined;
    export let _virtualImage: string | undefined;
    export let _currentDateIndex: number | undefined = 0;
    let _dashboardClient = new DashboardClient();
    let _scrollSkip = 1;
    let _currentDownloadStatus = "";
    let _filteredVirtualImages: Date[] = [];
    let _unsubscriber: Unsubscriber[] = [];
    let _downloadInfo: DownloadInfo[] = [];
    export let _segmentationParameter: SegmentationParameter[] = [];
    let _selectedSegmentation: SegmentationTemplate | undefined;
    let _savedSegmentation: SegmentationTemplate | undefined;
    let _showSegmentedImage: boolean = false;
    let _imageCache = new Map<string, {image: string; added: Date}>();

    onMount(async () => {
        _unsubscriber.push(
            _virtualImageFilterByTime.subscribe((value) => {
                if (value.size == 0) {
                    _filteredVirtualImages = _virtualImages.map((vi) => vi.creationDate);
                    return;
                }
                _filteredVirtualImages = pipe(value)
                    .orderBy((v) => v)
                    .apply((v) => v.map((x) => new Date(x)))
                    .toArray();
                if (_currentDateIndex == undefined || _currentDateIndex >= _filteredVirtualImages.length) _currentDateIndex = 0;
            })
        );
        _unsubscriber.push(_selectedTourChanged.subscribe((x) => selectedTourChanged(x)));
        if (_selectedTour == undefined) return;
        selectedTourChanged(_selectedTour);
    });
    onDestroy(() => {
        _unsubscriber.forEach((u) => u());
    });
    async function updateVirtualImage(tourId: number, updateSegmentation: boolean) {
        if (_currentDateIndex != undefined && _filteredVirtualImages.length > _currentDateIndex && _currentDateIndex >= 0) {
            const virtualImageTime = _filteredVirtualImages[_currentDateIndex!].getTime();
            _selectedImage = _virtualImages
                .map((vi) => ({
                    diff: Math.abs(vi.creationDate.getTime() - virtualImageTime),
                    image: vi
                }))
                .reduce((prev, curr) => (prev.diff < curr.diff ? prev : curr)).image;
            const cacheKey = JSON.stringify({
                name: _selectedImage.name,
                segmented: _showSegmentedImage,
                photoTourId: tourId,
                parameter: _showSegmentedImage ? _selectedSegmentation : ""
            });
            _virtualImage = "";
            if (updateSegmentation) findCorrespondingSegmentation(virtualImageTime);
            const cachedImage = _imageCache.get(cacheKey);
            if (cachedImage != undefined) {
                _virtualImage = cachedImage.image;
                _imageCache.set(cacheKey, {image: cachedImage.image, added: new Date()});
                return;
            }
            if (!_dashboardClient.tryRegisterRunning(cacheKey)) return;
            _virtualImage = _showSegmentedImage
                ? (await _dashboardClient.segmentedImage(_selectedImage.name, tourId, _selectedSegmentation)) ?? undefined
                : (await _dashboardClient.virtualImage(_selectedImage.name, tourId)) ?? undefined;
            if (_imageCache.size >= 30) {
                const entryToRemove = _imageCache.entries().reduce((prev, curr) => {
                    if (prev[1].added.getTime() < curr[1].added.getTime()) return prev;
                    return curr;
                })[0];
                _imageCache.delete(entryToRemove);
            }
            if (_virtualImage != undefined && _virtualImage.length > 0)
                _imageCache.set(cacheKey, {image: _virtualImage, added: new Date()});
        }
    }
    function findCorrespondingSegmentation(virtualImageTime: number) {
        _selectedSegmentation =
            _segmentationParameter.findLast((sp) => sp.tripTime <= new Date(virtualImageTime))?.template ??
            (_segmentationParameter.length > 0 ? _segmentationParameter[0].template : undefined);
        _selectedSegmentation = _selectedSegmentation?.clone();
    }

    async function nextImage(event: WheelEvent) {
        if (_selectedTour == undefined) return;
        _currentDateIndex =
            event.deltaY < 0
                ? Math.max(0, (_currentDateIndex ?? 0) - _scrollSkip)
                : Math.min(_filteredVirtualImages.length - 1, (_currentDateIndex ?? 0) + _scrollSkip);
        await updateVirtualImage(_selectedTour.id, true);
        if (_selectedImage == undefined) return;
    }
    async function selectedTourChanged(newTour: PhotoTourInfo | null) {
        if (newTour == null) return;
        _selectedTour = newTour;
        const dashboardClient = new DashboardClient();
        _virtualImages = pipe(await dashboardClient.virtualImageList(_selectedTour.id))
            .orderBy((vi) => vi.creationDate.getTime())
            .toArray();
        _filteredVirtualImages = _virtualImages.map((vi) => vi.creationDate);
        _currentDateIndex = _virtualImages.length == 0 ? undefined : _virtualImages.length - 1;
        await getSegmentationParameter();
        await updateVirtualImage(_selectedTour.id, true);
        updateDownloadStatus();
    }
    async function getSegmentationParameter() {
        if (_selectedTour == undefined) return;
        const dashboardClient = new DashboardClient();
        _segmentationParameter = await dashboardClient.plantMaskParameterFor(_selectedTour.id);
        _segmentationParameter = pipe(_segmentationParameter)
            .orderBy((sp) => sp.tripTime.getTime())
            .toArray();
    }

    async function updateDownloadStatus() {
        const dashboardClient = new DashboardClient();
        _downloadInfo = await dashboardClient.statusOfDownloadTourData();
        _currentDownloadStatus = downloadMessage();
    }

    async function downloadTourData() {
        const dashboardClient = new DashboardClient();
        if (_selectedTour == undefined) return;
        const info = _downloadInfo.find((di) => di.photoTourId == _selectedTour?.id);
        if (info?.readyToDownload) {
            Download.downloadFromUrl(dashboardClient.getBaseUrl("", "") + info.path);
            return;
        }
        if (info == undefined) {
            const tourData = await dashboardClient.requestDownloadTourData(_selectedTour?.id);
            _downloadInfo = _downloadInfo.filter((di) => di.photoTourId == _selectedTour?.id);
            _downloadInfo.push(tourData);
            _currentDownloadStatus = downloadMessage();
            return;
        }
        await updateDownloadStatus();
    }
    async function deletePackedData() {
        const dashboardClient = new DashboardClient();
        if (_selectedTour == undefined) return;
        await dashboardClient.deleteTourData(_selectedTour.id);
        updateDownloadStatus();
    }

    function downloadMessage() {
        if (_selectedTour == undefined) return "";
        const info = _downloadInfo.find((di) => di.photoTourId == _selectedTour!.id);
        if (info == undefined) return "Download Raw Data";
        if (info?.readyToDownload) return `Download ready (${info.sizeToDownloadInGb.toFixed(2)} GB)`;
        return `Compressing Status: ${info.currentSize.toFixed(2)}/${info.sizeToDownloadInGb.toFixed(2)} GB`;
    }
    async function updateSegmentation() {
        if (_selectedImage == undefined || _selectedTour == undefined || _selectedSegmentation == undefined) return;
        const dashboardClient = new DashboardClient();
        await dashboardClient.storeCustomSegmentation(_selectedSegmentation, _selectedImage.creationDate, _selectedTour.id);
        await getSegmentationParameter();
        _segmentationChanged.update((_) => _segmentationParameter);
        await updateVirtualImage(_selectedTour.id, true);
    }
    function segmentationParameterChanged() {
        if (_selectedTour == undefined || _selectedSegmentation == undefined) return;
        updateVirtualImage(_selectedTour.id, false);
    }
</script>

<div class="col-md-12 row mt-2">
    <slot />
    <div class="col-md-7">
        <div style="align-items:center" class="col-md-12 row mt-2">
            <div class="col-md-3">{_selectedImage?.creationDate.toLocaleString()}</div>
            <NumberInput class="col-md-2" bind:value={_scrollSkip} label="Show nth image"></NumberInput>
            <div class="col-md-2">
                Index: {_currentDateIndex == undefined
                    ? "None"
                    : Math.min(_currentDateIndex + 1, _filteredVirtualImages.length)}/{_filteredVirtualImages.length}
            </div>
            <div class="col-md-3 p-0 row ms-2">
                <button class="btn btn-primary col-md-9" on:click={downloadTourData}>{_currentDownloadStatus}</button>
                {#if _currentDownloadStatus.includes("ready")}
                    <div class="col-md-1"></div>
                    <button class="btn btn-danger col-md-2" on:click={deletePackedData}>X</button>
                {/if}
            </div>
            <Checkbox
                class="col-md-2"
                label="Segmentation"
                valueHasChanged={() => {
                    if (_selectedTour == undefined) return;
                    updateVirtualImage(_selectedTour?.id, true);
                }}
                bind:value={_showSegmentedImage}></Checkbox>
        </div>
    </div>
    <div style="min-height: 500px;flex-direction: row;flex:content;display:flex" class="p-0">
        {#if _virtualImage == ""}
            <div></div>
            <div on:wheel={nextImage} style="flex:auto;width:1000px"></div>
        {:else if _virtualImage != undefined}
            <img
                on:wheel={nextImage}
                style="max-width: 85%;max-height:79vh"
                alt="Stitched Result"
                src="data:image/png;base64,{_virtualImage}" />
            <div style="flex:auto;"></div>
        {:else}
            <div on:wheel={nextImage}>Scroll to change image</div>
            <div on:wheel={nextImage} style="flex:auto;width:1000px"></div>
        {/if}
        <div class="d-flex flex-column colm-3" style="width: 15%">
            {#if _selectedSegmentation != undefined && _showSegmentedImage && _selectedTour != undefined && _selectedTour != null}
                {@const tourId = _selectedTour.id}
                <TextInput bind:value={_selectedSegmentation.name} label="Segmentation Name"></TextInput>
                <div class="row">
                    <NumberInput
                        class="col-md-6"
                        valueHasChanged={segmentationParameterChanged}
                        label="Low Hue"
                        bind:value={_selectedSegmentation.hLow}></NumberInput>
                    <NumberInput
                        class="col-md-6"
                        valueHasChanged={segmentationParameterChanged}
                        label="High Hue"
                        bind:value={_selectedSegmentation.hHigh}></NumberInput>
                </div>
                <div class="row">
                    <NumberInput
                        class="col-md-6"
                        valueHasChanged={segmentationParameterChanged}
                        label="Low Sat."
                        bind:value={_selectedSegmentation.sLow}></NumberInput>
                    <NumberInput
                        class="col-md-6"
                        valueHasChanged={segmentationParameterChanged}
                        label="High Sat."
                        bind:value={_selectedSegmentation.sHigh}></NumberInput>
                </div>
                <div class="row">
                    <NumberInput
                        class="col-md-6"
                        valueHasChanged={segmentationParameterChanged}
                        label="Low Lum."
                        bind:value={_selectedSegmentation.lLow}></NumberInput>
                    <NumberInput
                        class="col-md-6"
                        valueHasChanged={segmentationParameterChanged}
                        label="High Lum."
                        bind:value={_selectedSegmentation.lHigh}></NumberInput>
                </div>
                <NumberInput
                    class="col-md-12"
                    valueHasChanged={segmentationParameterChanged}
                    label="Opening Iterations"
                    bind:value={_selectedSegmentation.openingIterations}></NumberInput>
                <Checkbox
                    valueHasChanged={segmentationParameterChanged}
                    label="Otsu Thresholding"
                    bind:value={_selectedSegmentation.useOtsu}></Checkbox>
                <button on:click={updateSegmentation} class="btn btn-primary">Update Segmentation</button>
                <div class="d-flex flex-column colm-2" style="height:30vh;overflow-y:auto">
                    <div class="d-flex flex-row justify-content-between">
                        <button
                            on:click={async () => {
                                _selectedSegmentation = _savedSegmentation?.clone();
                                await updateVirtualImage(tourId, false);
                            }}
                            disabled={_savedSegmentation == undefined}
                            class="col-md-5 btn btn-success">
                            Restore</button>
                        <button
                            on:click={() => (_savedSegmentation = _selectedSegmentation?.clone())}
                            class="col-md-5 btn btn-success">Save</button>
                    </div>
                    {#each _segmentationParameter as sp}
                        <button
                            on:click={async () => {
                                _selectedSegmentation = sp.template.clone();
                                await updateVirtualImage(tourId, false);
                            }}
                            class="btn btn-dark {JSON.stringify(sp.template) == JSON.stringify(_selectedSegmentation)
                                ? 'opacity-100'
                                : 'opacity-50'}">
                            {sp.template.name}
                        </button>
                    {/each}
                </div>
            {/if}
        </div>
    </div>
</div>
