<script lang="ts">
    import {Enum} from "~/types/Enum";
    import {SelectedDashboardTab, DasboardTabDescriptions} from "./SelectedDashboardTab";
    import VirtualImageViewer from "./VirtualImageViewer.svelte";
    import PlantSummary from "./PlantSummary.svelte";
    import {
        _onlyShowSelectedPlantsChanged,
        _selectedPlantsChanged,
        _selectedTourChanged,
        _virtualImageFilterByTime
    } from "./DashboardContext";
    import {onMount} from "svelte";
    import {AutomaticPhotoTourClient, PhotoTourInfo, VirtualImageInfo} from "~/services/GatewayAppApi";
    import {pipe} from "~/types/Pipe";
    import Checkbox from "../reuseableComponents/Checkbox.svelte";
    let _filteredIndices = 0;
    let _selectedTab: SelectedDashboardTab = SelectedDashboardTab.plantSummary;
    let _photoTours: PhotoTourInfo[] = [];
    let _selectedImage: VirtualImageInfo | undefined;
    let _virtualImage: string | undefined;
    let _currentDateIndex: number | undefined;
    onMount(async () => {
        $_virtualImageFilterByTime = new Set();
        _virtualImageFilterByTime.subscribe((value) => {
            _filteredIndices = value.size;
        });
        const automaticPhototourClient = new AutomaticPhotoTourClient();
        _photoTours = await automaticPhototourClient.getPhotoTours();
        _photoTours = pipe(_photoTours)
            .apply((x) => x.filter((pt) => pt.tripCount > 0))
            .orderByDescending((pt) => pt.lastEvent.getTime())
            .toArray();
    });
    function clearFilter() {
        _virtualImageFilterByTime.update((x) => {
            x.clear();
            return x;
        });
    }
    function removeSelectedImageFromFilter() {
        _virtualImageFilterByTime.update((vi) => {
            if (_selectedImage == undefined || vi.size == 0) return vi;
            const selectedTime = _selectedImage.creationDate.getTime() ?? 0;
            const timeToRemove = Array.from(vi)
                .map((x) => ({
                    diff: Math.abs(x - selectedTime),
                    time: x
                }))
                .toSorted((a, b) => a.diff - b.diff)[0];
            vi.delete(timeToRemove.time);
            return vi;
        });
        _selectedImage = undefined;
        _virtualImage = undefined;
        _currentDateIndex = undefined;
    }
    async function selectedTourChanged(tour: PhotoTourInfo) {
        $_selectedPlantsChanged = [];
        $_onlyShowSelectedPlantsChanged = false;
        _selectedTourChanged.update(() => tour);
    }
</script>

<svelte:head><title>Dashboard</title></svelte:head>

<div style="align-items: center;" class="row col-md-12 rowm-3">
    {#each Enum.getAllEntries(SelectedDashboardTab) as { value }}
        <button
            style="height: 30px;line-height: 0px;"
            on:click={() => (_selectedTab = value)}
            class="btn btn-dark col-md-2 {_selectedTab == value ? 'opacity-100' : 'opacity-50'}"
            >{DasboardTabDescriptions.get(value)}</button>
    {/each}
    <div class="col-md-3" style="align-items: center;">
        <Checkbox
            bind:value={$_onlyShowSelectedPlantsChanged}
            disabledSelector={() => $_selectedPlantsChanged.length == 0}
            label="Only selected {$_selectedPlantsChanged.length} Plants"
            class="col-md-12"></Checkbox>
        <div class="ms-5">Selected Times: {_filteredIndices}</div>
    </div>
    <button on:click={clearFilter} class="col-md-1 btn btn-danger">Clear</button>
    {#if _filteredIndices > 0 && _currentDateIndex != undefined}
        <button on:click={removeSelectedImageFromFilter} class="col-md-2 btn btn-danger">Remove {_currentDateIndex + 1}</button>
    {/if}
</div>
<hr class="col-md-12" />
<div style="display: {_selectedTab == SelectedDashboardTab.virtualPhotoViewer ? 'unset' : 'none'}">
    <VirtualImageViewer bind:_selectedImage bind:_virtualImage bind:_currentDateIndex>
        <div style="overflow-x:auto;white-space:nowrap" class="d-flex flex-row rowm-3 col-md-5">
            {#each _photoTours as tour}
                <button
                    on:click={async () => _selectedTourChanged.update(() => tour)}
                    style="height:40px"
                    class="btn btn-dark {tour == $_selectedTourChanged ? 'opacity-100' : 'opacity-50'}">{tour.name}</button>
            {/each}
        </div>
    </VirtualImageViewer>
</div>
<div style="display: {_selectedTab == SelectedDashboardTab.plantSummary ? 'unset' : 'none'}">
    <PlantSummary>
        <div style="overflow-x:auto;white-space:nowrap" class="d-flex flex-row rowm-3 col-md-5">
            {#each _photoTours as tour}
                <button
                    on:click={async () => await selectedTourChanged(tour)}
                    style="height:40px"
                    class="btn btn-dark {tour == $_selectedTourChanged ? 'opacity-100' : 'opacity-50'}">{tour.name}</button>
            {/each}
        </div>
    </PlantSummary>
</div>
