<script lang="ts">
    import {onDestroy, onMount} from "svelte";
    import {DashboardClient, DownloadInfo, PhotoTourInfo, SegmentationTemplate, VirtualImageInfo} from "~/services/GatewayAppApi";
    import NumberInput from "~/features/reuseableComponents/NumberInput.svelte";
    import {Download} from "~/types/Download";
    import {_selectedTourChanged, _virtualImageFilterByTime} from "./DashboardContext";
    import type {Unsubscriber} from "svelte/motion";
    import Checkbox from "../reuseableComponents/Checkbox.svelte";
    let _selectedTour: PhotoTourInfo | undefined | null;
    let _virtualImages: VirtualImageInfo[] = [];
    export let _selectedImage: VirtualImageInfo | undefined;
    export let _virtualImage: string | undefined;
    export let _currentDateIndex: number | undefined = 0;
    let _scrollSkip = 1;
    let _currentDownloadStatus = "";
    let _filteredVirtualImages: Date[] = [];
    let _unsubscriber: Unsubscriber[] = [];
    let _downloadInfo: DownloadInfo[] = [];
    let _segmentationParameter: SegmentationTemplate | undefined;
    let _showSegmentedImage: boolean = false;
    let _imageCache = new Map<string, {image: string; added: Date}>();

    onMount(async () => {
        _unsubscriber.push(
            _virtualImageFilterByTime.subscribe((value) => {
                if (value.size == 0) {
                    _filteredVirtualImages = _virtualImages.map((vi) => vi.creationDate);
                    return;
                }
                _filteredVirtualImages = Array.from(value)
                    .toSorted((a, b) => a - b)
                    .map((x) => new Date(x));
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
    async function updateVirtualImage(tourId: number) {
        const dashboardClient = new DashboardClient();
        _virtualImage = "";
        if (_currentDateIndex != undefined && _filteredVirtualImages.length > _currentDateIndex && _currentDateIndex >= 0) {
            _selectedImage = _virtualImages
                .map((vi) => ({
                    diff: Math.abs(vi.creationDate.getTime() - _filteredVirtualImages[_currentDateIndex!].getTime()),
                    image: vi
                }))
                .reduce((prev, curr) => (prev.diff < curr.diff ? prev : curr)).image;
            const cacheKey = JSON.stringify({
                name: _selectedImage.name,
                segmented: _showSegmentedImage,
                photoTourId: tourId,
                parameter: _showSegmentedImage ? _segmentationParameter : ""
            });
            const cachedImage = _imageCache.get(cacheKey);
            if (cachedImage != undefined) {
                _virtualImage = cachedImage.image;
                _imageCache.set(cacheKey, {image: cachedImage.image, added: new Date()});
                return;
            }
            _virtualImage = _showSegmentedImage
                ? (await dashboardClient.segmentedImage(_selectedImage.name, tourId, _segmentationParameter)) ?? undefined
                : (await dashboardClient.virtualImage(_selectedImage.name, tourId)) ?? undefined;
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
    async function nextImage(event: WheelEvent) {
        if (_selectedTour == undefined) return;
        _currentDateIndex =
            event.deltaY < 0
                ? Math.max(0, (_currentDateIndex ?? 0) - _scrollSkip)
                : Math.min(_filteredVirtualImages.length - 1, (_currentDateIndex ?? 0) + _scrollSkip);
        await updateVirtualImage(_selectedTour.id);
        if (_selectedImage == undefined) return;
        const dashboardClient = new DashboardClient();
        _segmentationParameter = await dashboardClient.plantMaskParameterFor(_selectedImage.creationDate, _selectedTour.id);
    }
    async function selectedTourChanged(newTour: PhotoTourInfo | null) {
        if (newTour == null) return;
        _selectedTour = newTour;
        const dashboardClient = new DashboardClient();
        _virtualImages = (await dashboardClient.virtualImageList(_selectedTour.id)).toSorted((a, b) =>
            a.creationDate.orderBy(b.creationDate)
        );
        _filteredVirtualImages = _virtualImages.map((vi) => vi.creationDate);
        _currentDateIndex = _virtualImages.length == 0 ? undefined : _virtualImages.length - 1;
        await updateVirtualImage(_selectedTour.id);
        _segmentationParameter = await dashboardClient.plantMaskParameterFor();
        updateDownloadStatus();
    }
    async function updateDownloadStatus() {
        const dashboardClient = new DashboardClient();
        _downloadInfo = await dashboardClient.statusOfDownloadTourData();
        _currentDownloadStatus = DownloadMessage();
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
            _currentDownloadStatus = DownloadMessage();
            return;
        }
        await updateDownloadStatus();
    }
    async function DeletePackedData() {
        const dashboardClient = new DashboardClient();
        if (_selectedTour == undefined) return;
        await dashboardClient.deleteTourData(_selectedTour.id);
        updateDownloadStatus();
    }

    function DownloadMessage() {
        if (_selectedTour == undefined) return "";
        const info = _downloadInfo.find((di) => di.photoTourId == _selectedTour!.id);
        if (info == undefined) return "Download Raw Data";
        if (info?.readyToDownload) return `Download ready (${info.sizeToDownloadInGb.toFixed(2)} GB)`;
        return `Compressing Status: ${info.currentSize.toFixed(2)}/${info.sizeToDownloadInGb.toFixed(2)} GB`;
    }
    async function UpdateSegmentation() {
        if (_selectedImage == undefined || _selectedTour == undefined || _segmentationParameter == undefined) return;
        const dashboardClient = new DashboardClient();
        dashboardClient.storeCustomSegmentation(_segmentationParameter, _selectedImage.creationDate, _selectedTour.id);
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
                    <button class="btn btn-danger col-md-2" on:click={DeletePackedData}>X</button>
                {/if}
            </div>
            <Checkbox
                class="col-md-2"
                label="Segmentation"
                valueHasChanged={() => {
                    if (_selectedTour == undefined) return;
                    updateVirtualImage(_selectedTour?.id);
                }}
                bind:value={_showSegmentedImage}></Checkbox>
        </div>
    </div>
    <div style="min-height: 500px;flex-direction: row;flex:content;display:flex" on:wheel={nextImage} class="p-0">
        {#if _virtualImage == ""}
            <div></div>
            <div style="flex:auto;width:1000px"></div>
        {:else if _virtualImage != undefined}
            <img style="max-width: 85%;max-height:79vh" alt="Stitched Result" src="data:image/png;base64,{_virtualImage}" />
            <div style="flex:auto;"></div>
        {:else}
            <div>Scroll to change image</div>
            <div style="flex:auto;width:1000px"></div>
        {/if}
        <div class="d-flex flex-column colm-3" style="width: 15%;">
            {#if _segmentationParameter != undefined && _showSegmentedImage && _selectedTour != undefined && _selectedTour != null}
                {@const tourId = _selectedTour.id}
                <NumberInput
                    valueHasChanged={() => updateVirtualImage(tourId)}
                    label="Low Hue"
                    bind:value={_segmentationParameter.hLow}></NumberInput>
                <NumberInput
                    valueHasChanged={() => updateVirtualImage(tourId)}
                    label="High Hue"
                    bind:value={_segmentationParameter.hHigh}></NumberInput>
                <NumberInput
                    valueHasChanged={() => updateVirtualImage(tourId)}
                    label="Low Saturation"
                    bind:value={_segmentationParameter.sLow}></NumberInput>
                <NumberInput
                    valueHasChanged={() => updateVirtualImage(tourId)}
                    label="High Saturation"
                    bind:value={_segmentationParameter.sHigh}></NumberInput>
                <NumberInput
                    valueHasChanged={() => updateVirtualImage(tourId)}
                    label="Low Lumination"
                    bind:value={_segmentationParameter.lLow}></NumberInput>
                <NumberInput
                    valueHasChanged={() => updateVirtualImage(tourId)}
                    label="High Lumination"
                    bind:value={_segmentationParameter.lHigh}></NumberInput>
                <NumberInput
                    valueHasChanged={() => updateVirtualImage(tourId)}
                    label="Opening Iterations"
                    bind:value={_segmentationParameter.openingIterations}></NumberInput>
                <Checkbox
                    valueHasChanged={() => updateVirtualImage(tourId)}
                    label="Otsu Thresholding"
                    bind:value={_segmentationParameter.useOtsu}></Checkbox>
                <button on:click={UpdateSegmentation} class="btn btn-primary">Update Segmentation</button>
            {/if}
        </div>
    </div>
</div>
