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
    import {_virtualImageFilterByTime} from "./DashboardContext";
    class DescriptorInfo {
        name: string;
        unit: string;
        validator: (value: number) => boolean;
        tooltipFormatter: (value: number) => string;
        getDescriptor: (descriptor: PlantImageDescriptors) => number;
    }
    let _photoTours: PhotoTourInfo[] = [];
    let _selectedTour: PhotoTourInfo | undefined;
    let _virtualImageSummaries: VirtualImageSummary[] = [];
    let _selectedPlants: string[] = [];
    let _selectedDescriptors: DescriptorInfo[] = [];
    let _chart: echarts.ECharts;
    let _chartData: {name: string; yAxisIndex: number; type: string; showSymbol: boolean; data: (number | Date)[][]}[] = [];
    let _descriptorBySeries: Map<string, DescriptorInfo> = new Map();
    const _graphId = Math.random().toString(36).substring(7);
    let _descriptorsFor: DescriptorInfo[] = [
        {
            name: "Convex Hull",
            unit: "mm²",
            tooltipFormatter: (value) => value.toFixed(1) + " mm²",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors) => descriptor.convexHullAreaInMm2
        },
        {
            name: "Approx. Leaf Count",
            unit: "count",
            tooltipFormatter: (value) => value + "",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors) => descriptor.leafCount
        },
        {
            name: "Plant Size",
            unit: "mm²",
            tooltipFormatter: (value) => value.toFixed(1) + " mm²",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors) => descriptor.sizeInMm2
        },
        {
            name: "Solidity",
            unit: "%",
            tooltipFormatter: (value) => value.toFixed(1) + "%",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors) => descriptor.solidity * 100
        },
        {
            name: "IR Temperature",
            unit: "°C",
            tooltipFormatter: (value) => value.toFixed(1) + "°C",
            validator: (x) => x > 0,
            getDescriptor: (descriptor: PlantImageDescriptors) => descriptor.averageTemperature
        },
        {
            name: "Extent",
            unit: "%",
            tooltipFormatter: (value) => value.toFixed(1) + "%",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors) => descriptor.extent * 100
        }
    ];

    onMount(async () => {
        const automaticPhototourClient = new AutomaticPhotoTourClient();
        _photoTours = await automaticPhototourClient.getPhotoTours();
        _photoTours = _photoTours.toSorted((a, b) => (a.lastEvent > b.lastEvent ? -1 : 1));
    });

    function updateChart() {
        if (_virtualImageSummaries.length == 0) return;
        _chart ??= echarts.init(document.getElementById(_graphId));
        const filteredSummaries = _virtualImageSummaries.filter((x) =>
            _selectedPlants.reduce(
                (a, p) => a && x.imageDescriptors.plantDescriptors.find((pd) => pd.plant.imageName == p) != undefined,
                true
            )
        );
        _chartData = [];
        _descriptorBySeries = new Map();
        for (let i = 0; i < _selectedPlants.length; i++) {
            const plant = _selectedPlants[i];
            for (let j = 0; j < _selectedDescriptors.length; j++) {
                const descriptor = _selectedDescriptors[j];
                const data = filteredSummaries
                    .map((x) => {
                        const descriptorValue = x.imageDescriptors.plantDescriptors.find((p) => p.plant.imageName == plant);
                        const value = descriptor.getDescriptor(descriptorValue!);
                        if (!descriptor.validator(value)) return [];
                        return [new Date(x.imageDescriptors.tripStart), value];
                    })
                    .filter((x) => x.length > 0);
                _descriptorBySeries.set(descriptor.name + " " + plant, descriptor);
                _chartData.push({
                    name: descriptor.name + " " + plant,
                    type: "line",
                    yAxisIndex: j,
                    showSymbol: false,
                    data: data
                });
            }
        }
        _chart.clear();
        let currentlyHoveredTimes: Date[] = [];
        _chart.getZr().on("click", (params) => {
            _virtualImageFilterByTime.update((x) => {
                currentlyHoveredTimes.map((t) => x.add(Math.round(t.getTime() / 1000)));
                return x;
            });
        });
        _chart.setOption({
            series: _chartData,
            legend: {left: "left"},
            animation: false,
            tooltip: {
                trigger: "axis",
                axisPointer: {animation: false},
                formatter: function (params: {seriesName: string; value: [Date, number]}[], x: any) {
                    currentlyHoveredTimes = params.map((p) => p.value[0]);
                    return (
                        params
                            .map((p, i) => ({
                                descriptor: _descriptorBySeries.get(p.seriesName),
                                value: p
                            }))
                            .map(
                                (x) =>
                                    '<span class="d-flex flex-row mb-2" style="width:300px">' +
                                    `<span class=\"col-md-8\">${x.value.seriesName}</span>` +
                                    `<span class=\"col-md-4\">${x.descriptor?.tooltipFormatter(x.value.value[1])}</span>` +
                                    "</span>"
                            )
                            .join("") + params[0].value[0].toLocaleString()
                    );
                }
            },
            toolbox: {
                feature: {
                    dataZoom: {yAxisIndex: "none"},
                    restore: {},
                    saveAsImage: {}
                }
            },
            dataZoom: [
                {show: true, realtime: true, xAxisIndex: [0, 1]},
                {type: "inside", realtime: true, xAxisIndex: [0, 1]}
            ],
            xAxis: {type: "time"},
            yAxis: _selectedDescriptors.map((d) => ({type: "value", name: d.name + " in " + d.unit}))
        });
        _chart.dispatchAction({
            type: "takeGlobalCursor",
            key: "dataZoomSelect",
            dataZoomSelectActive: true
        });
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
        else {
            _selectedDescriptors.push(descriptor);
            if (_selectedDescriptors.length > 2) _selectedDescriptors.shift();
        }
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
