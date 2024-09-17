<script lang="ts">
    import {Enum} from "~/types/Enum";
    import {SelectedDashboardTab, DasboardTabDescriptions} from "./SelectedDashboardTab";
    import VirtualImageViewer from "./VirtualImageViewer.svelte";
    import PlantSummary from "./PlantSummary.svelte";
    import {_selectedTourChanged, _virtualImageFilterByTime} from "./DashboardContext";
    import {onMount} from "svelte";
    import {AutomaticPhotoTourClient, PhotoTourInfo} from "~/services/GatewayAppApi";
    let _filteredIndices = 0;
    let _selectedTab: SelectedDashboardTab = SelectedDashboardTab.plantSummary;
    let _photoTours: PhotoTourInfo[] = [];
    onMount(async () => {
        $_virtualImageFilterByTime = new Set();
        _virtualImageFilterByTime.subscribe((value) => {
            _filteredIndices = value.size;
        });
        const automaticPhototourClient = new AutomaticPhotoTourClient();
        _photoTours = await automaticPhototourClient.getPhotoTours();
        _photoTours = _photoTours.toSorted((a, b) => a.lastEvent.orderByDescending(b.lastEvent));
    });
    function clearFilter() {
        _virtualImageFilterByTime.update((x) => {
            x.clear();
            return x;
        });
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
    <div class="col-md-3"></div>
    <div class="col-md-2">Filtered Indices: {_filteredIndices}</div>
    <button on:click={clearFilter} class="col-md-1 btn btn-danger">Clear</button>
    <button on:click={clearFilter} class="col-md-1 btn btn-danger">Remove selected</button>
</div>
<hr class="col-md-12" />
<div style="display: {_selectedTab == SelectedDashboardTab.virtualPhotoViewer ? 'unset' : 'none'}">
    <VirtualImageViewer>
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
                    on:click={async () => _selectedTourChanged.update(() => tour)}
                    style="height:40px"
                    class="btn btn-dark {tour == $_selectedTourChanged ? 'opacity-100' : 'opacity-50'}">{tour.name}</button>
            {/each}
        </div>
    </PlantSummary>
</div>
