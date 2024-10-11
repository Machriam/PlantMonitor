<script lang="ts">
    import * as echarts from "echarts";
    import {onDestroy, onMount} from "svelte";
    import {
        DashboardClient,
        PhotoTourInfo,
        PlantImageDescriptors,
        SegmentationParameter,
        VirtualImageSummary
    } from "~/services/GatewayAppApi";
    import {Download} from "~/types/Download";
    import {
        _segmentationChanged,
        _selectedPlantsChanged,
        _selectedTourChanged,
        _virtualImageFilterByTime
    } from "./DashboardContext";
    import type {Unsubscriber} from "svelte/store";
    import {pipe} from "~/types/Pipe";
    class DescriptorInfo {
        name: string;
        unit: string;
        validator: (value: number) => boolean;
        tooltipFormatter: (value: number) => string;
        getDescriptor: (descriptors: PlantImageDescriptors[]) => number;
        isGlobal: boolean;
    }
    let _selectedTour: PhotoTourInfo | null = null;
    let _virtualImageSummaries: VirtualImageSummary[] = [];
    let _selectedPlants: string[] = [];
    let _selectedDescriptors: DescriptorInfo[] = [];
    let _chart: echarts.ECharts | undefined;
    let _lastDataZoom: {start: number; end: number} = {start: 0, end: 100};
    export let _segmentationParameter: SegmentationParameter[] = [];
    let _currentlyHoveredTimes: Date[] = [];
    let _chartData: {
        name: string;
        yAxisIndex: number;
        type: string;
        showSymbol: boolean;
        markLine: {
            data: {
                symbol: string;
                name: string;
                xAxis: Date;
                y: number;
                itemStyle: {color: string};
            }[][];
        };
        markPoint: {
            data: {
                coord: (number | Date)[];
                y: string;
                symbol: string;
                symbolSize: number;
                symbolRotate: number;
                itemStyle: {color: string};
            }[];
        };
        data: (number | Date)[][];
    }[] = [];
    let _descriptorBySeries: Map<string, DescriptorInfo> = new Map();
    let _unsubscriber: Unsubscriber[] = [];
    let _minAbsoluteTime: number = 0;
    let _maxAbsoluteTime: number = 0;
    const _graphId = Math.random().toString(36).substring(7);
    let _descriptorsFor: DescriptorInfo[] = [
        {
            name: "Overlapping Leaves",
            unit: "count",
            tooltipFormatter: (value) => value + "",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) => pipe(descriptor).count((x) => x.leafOutOfRange),
            isGlobal: true
        },
        {
            name: "Avg. Temperature",
            unit: "°C",
            tooltipFormatter: (value) => value.toFixed(1) + " °C",
            validator: (x) => x > 0,
            getDescriptor: (descriptor: PlantImageDescriptors[]) =>
                pipe(descriptor)
                    .apply((x) => x.filter((y) => y.averageTemperature > 0))
                    .mean((x) => x.averageTemperature)
                    .valueOf(),
            isGlobal: true
        },
        {
            name: "Avg. Plant Size",
            unit: "mm²",
            tooltipFormatter: (value) => value.toFixed(1) + " mm²",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) =>
                pipe(descriptor)
                    .mean((x) => x.sizeInMm2)
                    .valueOf(),
            isGlobal: true
        },
        {
            name: "Avg. Plant Solidity",
            unit: "%",
            tooltipFormatter: (value) => value.toFixed(1) + "%",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) =>
                pipe(descriptor)
                    .mean((x) => x.solidity)
                    .valueOf(),
            isGlobal: true
        },
        {
            name: "Avg. Plant Extent",
            unit: "%",
            tooltipFormatter: (value) => value.toFixed(1) + "%",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) =>
                pipe(descriptor)
                    .mean((x) => x.extent)
                    .valueOf(),
            isGlobal: true
        },
        {
            name: "Avg. Hue",
            unit: "°",
            tooltipFormatter: (value) => value.toFixed(1) + "°",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) =>
                pipe(descriptor)
                    .mean((x) => x.hslAverage[0])
                    .valueOf(),
            isGlobal: true
        },
        {
            name: "Avg. Saturation",
            unit: "%",
            tooltipFormatter: (value) => value.toFixed(1) + "%",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) =>
                pipe(descriptor)
                    .mean((x) => x.hslAverage[1])
                    .valueOf() * 100,
            isGlobal: true
        },
        {
            name: "Avg. Lightness",
            unit: "%",
            tooltipFormatter: (value) => value.toFixed(1) + " %",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) =>
                pipe(descriptor)
                    .mean((x) => x.hslAverage[2])
                    .valueOf() * 100,
            isGlobal: true
        },
        {
            name: "Convex Hull",
            unit: "mm²",
            tooltipFormatter: (value) => value.toFixed(1) + " mm²",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) => descriptor[0].convexHullAreaInMm2,
            isGlobal: false
        },
        {
            name: "Approx. Leaf Count",
            unit: "count",
            tooltipFormatter: (value) => value + "",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) => descriptor[0].leafCount,
            isGlobal: false
        },
        {
            name: "Plant Size",
            unit: "mm²",
            tooltipFormatter: (value) => value.toFixed(1) + " mm²",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) => descriptor[0].sizeInMm2,
            isGlobal: false
        },
        {
            name: "Solidity",
            unit: "%",
            tooltipFormatter: (value) => value.toFixed(1) + "%",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) => descriptor[0].solidity * 100,
            isGlobal: false
        },
        {
            name: "IR Temperature",
            unit: "°C",
            tooltipFormatter: (value) => value.toFixed(1) + "°C",
            validator: (x) => x > 0,
            getDescriptor: (descriptor: PlantImageDescriptors[]) => descriptor[0].averageTemperature,
            isGlobal: false
        },
        {
            name: "Extent",
            unit: "%",
            tooltipFormatter: (value) => value.toFixed(1) + "%",
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) => descriptor[0].extent * 100,
            isGlobal: false
        },
        {
            name: "Hue",
            unit: "°",
            tooltipFormatter: (value) => value.toFixed(1),
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) => descriptor[0].hslAverage[0],
            isGlobal: false
        },
        {
            name: "Saturation",
            unit: "%",
            tooltipFormatter: (value) => value.toFixed(1),
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) => descriptor[0].hslAverage[1] * 100,
            isGlobal: false
        },
        {
            name: "Lightness",
            unit: "%",
            tooltipFormatter: (value) => value.toFixed(1),
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) => descriptor[0].hslAverage[2] * 100,
            isGlobal: false
        },
        {
            name: "Hue-Range",
            unit: "°",
            tooltipFormatter: (value) => value.toFixed(1),
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) => descriptor[0].hslMax[0] - descriptor[0].hslMin[0],
            isGlobal: false
        },
        {
            name: "Saturation-Range",
            unit: "%",
            tooltipFormatter: (value) => value.toFixed(1),
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) => (descriptor[0].hslMax[1] - descriptor[0].hslMin[1]) * 100,
            isGlobal: false
        },
        {
            name: "Lightness-Range",
            unit: "%",
            tooltipFormatter: (value) => value.toFixed(1),
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) => (descriptor[0].hslMax[2] - descriptor[0].hslMin[2]) * 100,
            isGlobal: false
        },
        {
            name: "Hue-Deviation",
            unit: "°",
            tooltipFormatter: (value) => value.toFixed(1),
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) => descriptor[0].hslDeviation[0],
            isGlobal: false
        },
        {
            name: "Saturation-Deviation",
            unit: "%",
            tooltipFormatter: (value) => value.toFixed(1),
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) => descriptor[0].hslDeviation[1] * 100,
            isGlobal: false
        },
        {
            name: "Lightness-Deviation",
            unit: "%",
            tooltipFormatter: (value) => value.toFixed(1),
            validator: (x) => true,
            getDescriptor: (descriptor: PlantImageDescriptors[]) => descriptor[0].hslDeviation[2] * 100,
            isGlobal: false
        }
    ];

    onMount(async () => {
        _unsubscriber.push(_selectedTourChanged.subscribe((x) => selectedTourChanged(x)));
        _unsubscriber.push(_virtualImageFilterByTime.subscribe(() => updateMarkers()));
        _unsubscriber.push(
            _segmentationChanged.subscribe((x) => {
                if (_selectedTour == undefined) return;
                _segmentationParameter = x;
                updateMarkers();
            })
        );
    });
    onDestroy(() => {
        _unsubscriber.forEach((u) => u());
    });
    function initChart() {
        const chart = echarts.init(document.getElementById(_graphId), null, {renderer: "svg"});
        chart.getZr().on("click", () => {
            _virtualImageFilterByTime.update((x) => {
                _currentlyHoveredTimes.map((t) => x.add(t.getTime()));
                updateMarkers();
                return x;
            });
        });
        chart.on("datazoom", (e) => {
            const zoom = e as {
                batch: {
                    startValue: number | undefined;
                    endValue: number | undefined;
                    start: number | undefined;
                    end: number | undefined;
                }[];
                start: number | undefined;
                end: number | undefined;
            };
            if (zoom.start != undefined && zoom.end != undefined) {
                _lastDataZoom.start = zoom.start;
                _lastDataZoom.end = zoom.end;
                return;
            }
            if (zoom.batch.length == 0) return;
            _lastDataZoom.start = zoom.batch[0].start ?? 0;
            _lastDataZoom.end = zoom.batch[0].end ?? 100;
            if (zoom.batch[0].startValue == undefined || zoom.batch[0].endValue == undefined) return;
            _lastDataZoom.start = ((zoom.batch[0].startValue - _minAbsoluteTime) / (_maxAbsoluteTime - _minAbsoluteTime)) * 100;
            _lastDataZoom.end = ((zoom.batch[0].endValue - _minAbsoluteTime) / (_maxAbsoluteTime - _minAbsoluteTime)) * 100;
        });
        return chart;
    }

    function updateChart() {
        if (_virtualImageSummaries.length == 0) return;
        _chart ??= initChart();
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
                if (descriptor.isGlobal) continue;
                const data = filteredSummaries
                    .map((x) => {
                        const descriptorValue = x.imageDescriptors.plantDescriptors.find((p) => p.plant.imageName == plant);
                        const value = descriptor.getDescriptor([descriptorValue!]);
                        if (!descriptor.validator(value)) return [];
                        return [new Date(x.imageDescriptors.tripEnd), value];
                    })
                    .filter((x) => x.length > 0);
                _descriptorBySeries.set(descriptor.name + " " + plant, descriptor);
                _chartData.push({
                    name: descriptor.name + " " + plant,
                    type: "line",
                    yAxisIndex: j,
                    markPoint: {data: []},
                    markLine: {data: []},
                    showSymbol: false,
                    data: data
                });
            }
        }
        for (let j = 0; j < _selectedDescriptors.length; j++) {
            if (!_selectedDescriptors[j].isGlobal) continue;
            const descriptor = _selectedDescriptors[j];
            const descriptorName = descriptor.name;
            _descriptorBySeries.set(descriptorName, descriptor);
            const data = filteredSummaries
                .map((x) => {
                    const descriptorValue = x.imageDescriptors.plantDescriptors;
                    const value = descriptor.getDescriptor(descriptorValue);
                    if (!descriptor.validator(value)) return [];
                    return [new Date(x.imageDescriptors.tripEnd), value];
                })
                .filter((x) => x.length > 0);
            _chartData.push({
                name: descriptorName,
                type: "line",
                yAxisIndex: j,
                markPoint: {data: []},
                markLine: {data: []},
                showSymbol: false,
                data: data
            });
        }
        _chart.clear();
        if (_chartData.length == 0) return;
        const times = _chartData.flatMap((cd) => cd.data.map((d) => (d[0] as Date).getTime()));
        _minAbsoluteTime = Math.min(...times);
        _maxAbsoluteTime = Math.max(...times);
        _chart.setOption({
            series: _chartData,
            legend: {left: "left"},
            animation: false,
            tooltip: {
                trigger: "axis",
                axisPointer: {animation: false},
                formatter: function (params: {seriesName: string; value: [Date, number]}[], x: any) {
                    _currentlyHoveredTimes = params.map((p) => p.value[0]);
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
                            .join("") + pipe(params[0].value[0]).formatDate()
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
                {show: true, realtime: true, xAxisIndex: [0, 1], start: _lastDataZoom.start, end: _lastDataZoom.end},
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
        updateMarkers();
    }
    function updateMarkers() {
        const times = Array.from($_virtualImageFilterByTime);
        if (_chartData.length == 0 || _chart == undefined) return;
        _chart.setOption({
            series: _chartData.map((s, i) => {
                s.markPoint.data =
                    i != 0
                        ? []
                        : s.data
                              .filter((d) => times.find((t) => t == (d[0] as Date).getTime()) != undefined)
                              .map((d) => ({
                                  coord: d,
                                  y: "10%",
                                  symbol: "arrow",
                                  symbolSize: 10,
                                  symbolRotate: 180,
                                  itemStyle: {color: "black"}
                              }));
                s.markLine.data =
                    _segmentationParameter.length <= 1 || i > 0
                        ? []
                        : _segmentationParameter.map((sp) => [
                              {
                                  symbol: "none",
                                  name: sp.template.name,
                                  xAxis: sp.tripTime,
                                  y: 60,
                                  itemStyle: {color: "black"}
                              },
                              {
                                  symbol: "none",
                                  name: "",
                                  xAxis: sp.tripTime,
                                  y: _chart!.getHeight() * 0.91,
                                  itemStyle: {color: "black"}
                              }
                          ]);
                return s;
            })
        });
    }
    async function selectedTourChanged(newTour: PhotoTourInfo | null) {
        _selectedTour = newTour;
        if (newTour == null) return;
        const dashboardClient = new DashboardClient();
        _segmentationParameter = await dashboardClient.plantMaskParameterFor(newTour.id);
        _virtualImageSummaries = await dashboardClient.summaryForTour(newTour.id);
        _virtualImageSummaries = _virtualImageSummaries.toSorted(
            (a, b) => a.imageDescriptors.tripStart.getTime() - b.imageDescriptors.tripStart.getTime()
        );
        _chart?.dispose();
        _chart = undefined;
        _selectedPlants = [];
        _selectedDescriptors = [];
        $_virtualImageFilterByTime = new Set();
    }
    async function downloadSummaryData() {
        if (_selectedTour == null) return;
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
        $_selectedPlantsChanged = _selectedPlants;
    }
    async function recalculateSummary() {
        if (_selectedTour == undefined) return;
        const dashboardClient = new DashboardClient();
        dashboardClient.recalculateImageSummaries(_selectedTour.id);
    }
</script>

<div class="col-md-12 row mt-2">
    <slot />
    <div style="align-items: center;" class="col-md-7 row mb-2">
        <div class="col-md-2"></div>
        <button disabled={_selectedTour == null} on:click={recalculateSummary} class="btn btn-primary col-md-3"
            >Recalculate Summary</button>
        <div class="col-md-1"></div>
        <button disabled={_selectedTour == null} on:click={downloadSummaryData} class="btn btn-primary col-md-2"
            >Download Data</button>
        <div class="col-md-1"></div>
        <div class="col-md-3">Summary Count: {_virtualImageSummaries.length}</div>
    </div>
    {#if _virtualImageSummaries.length > 0}
        {@const lastGlobalDescriptorIndex = _descriptorsFor.findLastIndex((d) => d.isGlobal)}
        {@const descriptors = pipe(_virtualImageSummaries).last().imageDescriptors.plantDescriptors}
        <div class="col-md-10 d-flex flex-column">
            <div style="height: 75vh;" id={_graphId}></div>
        </div>
        <div class="col-md-1 d-flex flex-column border-start p-0" style="height: 70vh;overflow-y:auto">
            {#each _descriptorsFor as descriptor}
                <button
                    on:click={() => toggleDescriptorSelection(descriptor)}
                    class="btn {_selectedDescriptors.findIndex((d) => d.name == descriptor.name) >= 0
                        ? 'bg-info bg-opacity-50'
                        : ''}">
                    {descriptor.name}
                </button>
                {#if _descriptorsFor[lastGlobalDescriptorIndex] == descriptor}
                    <hr />
                {/if}
            {/each}
        </div>
        <div class="col-md-1 d-flex flex-column border-start p-0" style="height: 70vh;overflow-y:auto">
            {#each pipe(descriptors)
                .apply((d) => d.map((p) => p.plant.imageName))
                .toArray()
                .toSorted() as plant}
                <button
                    on:click={() => togglePlant(plant)}
                    class="btn {_selectedPlants.findIndex((p) => p == plant) >= 0 ? 'bg-info bg-opacity-50' : ''}">
                    {plant}
                </button>
            {/each}
        </div>
    {/if}
</div>
