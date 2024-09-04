<script lang="ts">
    import {onMount} from "svelte";
    import {AutomaticPhotoTourClient, DashboardClient, DownloadInfo, PhotoTourInfo} from "~/services/GatewayAppApi";
    import NumberInput from "~/features/reuseableComponents/NumberInput.svelte";
    import {Download} from "~/types/Download";
    let _photoTours: PhotoTourInfo[] = [];
    let _selectedTour: PhotoTourInfo | undefined;
    let _virtualImages: string[] = [];
    let _selectedImage: string | undefined;
    let _virtualImage: string | undefined;
    let _currentImageIndex = 0;
    let _scrollSkip = 1;
    let _currentDownloadStatus = "";
    let _downloadInfo: DownloadInfo[] = [];

    onMount(async () => {
        const automaticPhototourClient = new AutomaticPhotoTourClient();
        _photoTours = await automaticPhototourClient.getPhotoTours();
        _photoTours = _photoTours.toSorted((a, b) => (a.lastEvent > b.lastEvent ? -1 : 1));
        _selectedTour = _photoTours.length > 0 ? _photoTours[0] : undefined;
        if (_selectedTour == undefined) return;
        selectedTourChanged(_selectedTour);
    });
    async function updateVirtualImage(tourId: number) {
        const dashboardClient = new DashboardClient();
        _virtualImage = undefined;
        if (_virtualImages.length > _currentImageIndex && _currentImageIndex >= 0) {
            _selectedImage = _virtualImages[_currentImageIndex];
            _virtualImage = await dashboardClient.virtualImage(_selectedImage, tourId);
        }
    }
    async function nextImage(event: WheelEvent) {
        if (_selectedTour == undefined) return;
        _currentImageIndex =
            event.deltaY < 0
                ? Math.max(0, _currentImageIndex - _scrollSkip)
                : Math.min(_virtualImages.length - 1, _currentImageIndex + _scrollSkip);
        await updateVirtualImage(_selectedTour.id);
    }
    async function selectedTourChanged(newTour: PhotoTourInfo) {
        _selectedTour = newTour;
        const dashboardClient = new DashboardClient();
        _virtualImages = (await dashboardClient.virtualImageList(_selectedTour.id)).toSorted().toReversed();
        _currentImageIndex = 0;
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
    <div style="width: 80vw;overflow-x:auto " class="d-flex flex-row rowm-3">
        {#each _photoTours as tour}
            <button
                on:click={async () => await selectedTourChanged(tour)}
                class="btn btn-dark {tour.name == _selectedTour?.name ? 'opacity-100' : 'opacity-50'}">{tour.name}</button>
        {/each}
    </div>
    <div on:wheel={nextImage} class="p-0" style="height: 80vh; width:80vw">
        <div style="align-items:center" class="col-md-12 row mt-2">
            <div class="col-md-3">{_virtualImages[_currentImageIndex]}</div>
            <div class="col-md-3">
                Index: {Math.min(_currentImageIndex + 1, _virtualImages.length)} of {_virtualImages.length}
            </div>
            <NumberInput class="col-md-2" bind:value={_scrollSkip} label="Show every nth image"></NumberInput>
            <div class="col-md-1"></div>
            <div class="col-md-3 p-0 row">
                <button class="btn btn-primary col-md-9" on:click={downloadTourData}>{_currentDownloadStatus}</button>
                {#if _currentDownloadStatus.includes("ready")}
                    <div class="col-md-1"></div>
                    <button class="btn btn-danger col-md-2" on:click={DeletePackedData}>X</button>
                {/if}
            </div>
        </div>
        {#if _virtualImage != undefined}
            <img style="max-width: 100%;max-height:100%" alt="Stitched Result" src="data:image/png;base64,{_virtualImage}" />
        {/if}
    </div>
</div>
