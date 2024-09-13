<script lang="ts">
    import {Enum} from "~/types/Enum";
    import {SelectedDashboardTab, DasboardTabDescriptions} from "./SelectedDashboardTab";
    import VirtualImageViewer from "./VirtualImageViewer.svelte";
    import PlantSummary from "./PlantSummary.svelte";
    import {_virtualImageFilterByTime} from "./DashboardContext";
    import {onMount} from "svelte";
    let _filteredIndices = 0;
    let _selectedTab: SelectedDashboardTab = SelectedDashboardTab.plantSummary;
    onMount(() => {
        $_virtualImageFilterByTime = new Set();
        _virtualImageFilterByTime.subscribe((value) => {
            _filteredIndices = value.size;
        });
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
    <button disabled={_selectedTab == SelectedDashboardTab.virtualPhotoViewer} class="col-md-1 btn btn-dark">Add Range</button>
    <button on:click={clearFilter} class="col-md-1 btn btn-danger">Clear</button>
</div>
<hr class="col-md-12" />
<div style="display: {_selectedTab == SelectedDashboardTab.virtualPhotoViewer ? 'unset' : 'none'}">
    <VirtualImageViewer></VirtualImageViewer>
</div>
<div style="display: {_selectedTab == SelectedDashboardTab.plantSummary ? 'unset' : 'none'}">
    <PlantSummary></PlantSummary>
</div>
