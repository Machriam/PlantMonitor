<script lang="ts">
    import * as echarts from "echarts";
    import {onMount} from "svelte";
    import {
        AutomaticPhotoTourClient,
        DashboardClient,
        PhotoTourInfo,
        PlantImageDescriptors,
        VirtualImageSummary
    } from "~/services/GatewayAppApi";
    import {Download} from "~/types/Download";
    class DescriptorInfo {
        name: string;
        getDescriptor: (descriptor: PlantImageDescriptors) => number;
    }
    let _photoTours: PhotoTourInfo[] = [];
    let _selectedTour: PhotoTourInfo | undefined;
    let _virtualImageSummaries: VirtualImageSummary[] = [];
    let _selectedPlants: string[] = [];
    let _selectedDescriptors: DescriptorInfo[] = [];
    let _chart: echarts.ECharts;
    const _graphId = Math.random().toString(36).substring(7);
    let _descriptorsFor: DescriptorInfo[] = [
        {name: "Convex Hull", getDescriptor: (descriptor: PlantImageDescriptors) => descriptor.convexHullAreaInMm2},
        {name: "Approx. Leaf Count", getDescriptor: (descriptor: PlantImageDescriptors) => descriptor.leafCount},
        {name: "Plant Size", getDescriptor: (descriptor: PlantImageDescriptors) => descriptor.sizeInMm2},
        {name: "Solidity", getDescriptor: (descriptor: PlantImageDescriptors) => descriptor.solidity},
        {name: "IR Temperature", getDescriptor: (descriptor: PlantImageDescriptors) => descriptor.averageTemperature},
        {name: "Extent", getDescriptor: (descriptor: PlantImageDescriptors) => descriptor.extent}
    ];

    onMount(async () => {
        const automaticPhototourClient = new AutomaticPhotoTourClient();
        _photoTours = await automaticPhototourClient.getPhotoTours();
        _photoTours = _photoTours.toSorted((a, b) => (a.lastEvent > b.lastEvent ? -1 : 1));
    });

    function updateChart() {
        if (_virtualImageSummaries.length == 0) return;
        _chart ??= echarts.init(document.getElementById(_graphId));
        const newData: {name: string; type: string; showSymbol: boolean; data: (number | Date)[][]}[] = [];
        for (let i = 0; i < _selectedPlants.length; i++) {
            const plant = _selectedPlants[i];
            for (let j = 0; j < _selectedDescriptors.length; j++) {
                const descriptor = _selectedDescriptors[j];
                const data = _virtualImageSummaries.map((x) => {
                    const descriptorValue = x.imageDescriptors.plantDescriptors.find((p) => p.plant.imageName == plant);
                    return [new Date(x.imageDescriptors.tripStart), descriptor.getDescriptor(descriptorValue!)];
                });
                newData.push({
                    name: plant + " " + descriptor.name,
                    type: "line",
                    showSymbol: false,
                    data: data
                });
            }
        }
        _chart.clear();
        _chart.setOption({
            title: {text: newData.map((x) => x.name).join(", ")},
            series: [],
            animation: false,
            tooltip: {
                trigger: "axis",
                axisPointer: {animation: false},
                formatter: function (params: {seriesName: string; value: [Date, number]}[]) {
                    return (
                        params.map((x) => x.seriesName + ": " + x.value[1].toFixed(1) + "°C").join("<br>") +
                        "<br>" +
                        params[0].value[0].toLocaleString()
                    );
                }
            },
            toolbox: {feature: {dataZoom: {yAxisIndex: "none"}, restore: {}, saveAsImage: {}}},
            dataZoom: [
                {show: true, realtime: true, xAxisIndex: [0, 1]},
                {type: "inside", realtime: true, xAxisIndex: [0, 1]}
            ],
            xAxis: {type: "time"},
            yAxis: {type: "value"}
        });
        _chart.setOption({series: newData});
    }
    async function selectedTourChanged(newTour: PhotoTourInfo) {
        _selectedTour = newTour;
        const dashboardClient = new DashboardClient();
        _virtualImageSummaries = await dashboardClient.summaryForTour(newTour.id);
    }
    async function downloadSummaryData() {
        if (_selectedTour == undefined) return;
        const dashboardClient = new DashboardClient();
        const url = await dashboardClient.createPhotoSummaryExport(_selectedTour.id);
        Download.downloadFromUrl(dashboardClient.getBaseUrl("", "") + url);
    }
    function toggleDescriptorSelection(descriptor: DescriptorInfo) {
        const index = _selectedDescriptors.findIndex((d) => d.name == descriptor.name);
        if (index >= 0) _selectedDescriptors.splice(index, 1);
        else _selectedDescriptors.push(descriptor);
        _selectedDescriptors = _selectedDescriptors;
        updateChart();
    }
    function togglePlant(plant: string) {
        const index = _selectedPlants.findIndex((p) => p == plant);
        if (index >= 0) _selectedPlants.splice(index, 1);
        else _selectedPlants.push(plant);
        _selectedPlants = _selectedPlants;
        updateChart();
    }
</script>

<div class="col-md-12 row mt-2">
    <div style="width: 60vw;overflow-x:auto " class="d-flex flex-row rowm-3 mb-2">
        {#each _photoTours as tour}
            <button
                on:click={async () => await selectedTourChanged(tour)}
                class="btn btn-dark {tour.name == _selectedTour?.name ? 'opacity-100' : 'opacity-50'}">{tour.name}</button>
        {/each}
    </div>
    <div style="align-items: center;" class="col-md-3 row mb-2">
        <div class="col-md-6">Summary Count: {_virtualImageSummaries.length}</div>
        <button disabled={_selectedTour == undefined} on:click={downloadSummaryData} class="btn btn-primary col-md-6"
            >Download Data</button>
    </div>
    {#if _virtualImageSummaries.length > 0}
        <div class="col-md-10 d-flex flex-column">
            <div style="height: 80vh; width:100%" id={_graphId}></div>
        </div>
        <div class="col-md-1 d-flex flex-column border-start" style="height: 70vh;overflow-y:auto">
            {#each _descriptorsFor as descriptor}
                <button
                    on:click={() => toggleDescriptorSelection(descriptor)}
                    class="btn {_selectedDescriptors.findIndex((d) => d.name == descriptor.name) >= 0
                        ? 'bg-info bg-opacity-50'
                        : ''}">
                    {descriptor.name}
                </button>
            {/each}
        </div>
        <div class="col-md-1 d-flex flex-column border-start" style="height: 70vh;overflow-y:auto">
            {#each _virtualImageSummaries[0].imageDescriptors.plantDescriptors.map((p) => p.plant.imageName).toSorted() as plant}
                <button
                    on:click={() => togglePlant(plant)}
                    class="btn {_selectedPlants.findIndex((p) => p == plant) >= 0 ? 'bg-info bg-opacity-50' : ''}">
                    {plant}
                </button>
            {/each}
        </div>
    {/if}
</div>
