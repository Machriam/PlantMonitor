<script lang="ts">
    import {onDestroy, onMount} from "svelte";
    import {DashboardClient, DownloadInfo, PhotoTourInfo, VirtualImageInfo} from "~/services/GatewayAppApi";
    import NumberInput from "~/features/reuseableComponents/NumberInput.svelte";
    import {Download} from "~/types/Download";
    import {_selectedTourChanged, _virtualImageFilterByTime} from "./DashboardContext";
    import type {Unsubscriber} from "svelte/motion";
    let _selectedTour: PhotoTourInfo | undefined | null;
    let _virtualImages: VirtualImageInfo[] = [];
    let _selectedImage: VirtualImageInfo | undefined;
    let _virtualImage: string | undefined;
    let _currentDateIndex = 0;
    let _scrollSkip = 1;
    let _currentDownloadStatus = "";
    let _filteredVirtualImages: Date[] = [];
    let _unsubscriber: Unsubscriber[] = [];
    let _downloadInfo: DownloadInfo[] = [];

    onMount(async () => {
        _unsubscriber.push(
            _virtualImageFilterByTime.subscribe((value) => {
                if (value.size == 0) {
                    _filteredVirtualImages = _virtualImages.map((vi) => vi.creationDate);
                    return;
                }
                _filteredVirtualImages = Array.from(value)
                    .toSorted((a, b) => b - a)
                    .map((x) => new Date(x));
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
        _virtualImage = undefined;
        if (_filteredVirtualImages.length > _currentDateIndex && _currentDateIndex >= 0) {
            _selectedImage = _virtualImages
                .map((vi) => ({
                    diff: Math.abs(vi.creationDate.getTime() - _filteredVirtualImages[_currentDateIndex].getTime()),
                    image: vi
                }))
                .reduce((prev, curr) => (prev.diff < curr.diff ? prev : curr)).image;
            _virtualImage = await dashboardClient.virtualImage(_selectedImage.name, tourId);
        }
    }
    async function nextImage(event: WheelEvent) {
        if (_selectedTour == undefined) return;
        _currentDateIndex =
            event.deltaY < 0
                ? Math.max(0, _currentDateIndex - _scrollSkip)
                : Math.min(_filteredVirtualImages.length - 1, _currentDateIndex + _scrollSkip);
        await updateVirtualImage(_selectedTour.id);
    }
    async function selectedTourChanged(newTour: PhotoTourInfo | null) {
        if (newTour == null) return;
        _selectedTour = newTour;
        const dashboardClient = new DashboardClient();
        _virtualImages = (await dashboardClient.virtualImageList(_selectedTour.id)).toSorted((a, b) =>
            a.creationDate.orderByDescending(b.creationDate)
        );
        _filteredVirtualImages = _virtualImages.map((vi) => vi.creationDate);
        _currentDateIndex = 0;
        await updateVirtualImage(_selectedTour.id);
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
</script>

<div class="col-md-12 row mt-2">
    <slot />
    <div class="col-md-7">
        <div style="align-items:center" class="col-md-12 row mt-2">
            <div class="col-md-3">{_selectedImage?.creationDate.toLocaleString()}</div>
            <div class="col-md-3">
                Index: {Math.min(_currentDateIndex + 1, _filteredVirtualImages.length)} of {_filteredVirtualImages.length}
            </div>
            <NumberInput class="col-md-3" bind:value={_scrollSkip} label="Show every nth image"></NumberInput>
            <div class="col-md-3 p-0 row ms-2">
                <button class="btn btn-primary col-md-9" on:click={downloadTourData}>{_currentDownloadStatus}</button>
                {#if _currentDownloadStatus.includes("ready")}
                    <div class="col-md-1"></div>
                    <button class="btn btn-danger col-md-2" on:click={DeletePackedData}>X</button>
                {/if}
            </div>
        </div>
    </div>
    <div on:wheel={nextImage} class="p-0">
        {#if _virtualImage != undefined}
            <img style="max-width: 100%;max-height:74vh" alt="Stitched Result" src="data:image/png;base64,{_virtualImage}" />
        {/if}
    </div>
</div>
