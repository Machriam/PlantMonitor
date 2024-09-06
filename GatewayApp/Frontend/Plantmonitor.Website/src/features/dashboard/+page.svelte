<script lang="ts">
    import {Enum} from "~/types/Enum";
    import {SelectedDashboardTab, DasboardTabDescriptions} from "./SelectedDashboardTab";
    import VirtualImageViewer from "./VirtualImageViewer.svelte";
    import PlantSummary from "./PlantSummary.svelte";
    let _selectedTab: SelectedDashboardTab = SelectedDashboardTab.plantSummary;
</script>

<svelte:head><title>Dashboard</title></svelte:head>

<div class="row col-md-12 rowm-3">
    {#each Enum.getAllEntries(SelectedDashboardTab) as { value }}
        <button
            style="height: 30px;line-height: 0px;"
            on:click={() => (_selectedTab = value)}
            class="btn btn-dark col-md-2 {_selectedTab == value ? 'opacity-100' : 'opacity-50'}"
            >{DasboardTabDescriptions.get(value)}</button>
    {/each}
</div>
<hr class="col-md-12" />
{#if _selectedTab == SelectedDashboardTab.virtualPhotoViewer}
    <VirtualImageViewer></VirtualImageViewer>
{/if}
{#if _selectedTab == SelectedDashboardTab.plantSummary}
    <PlantSummary></PlantSummary>
{/if}
