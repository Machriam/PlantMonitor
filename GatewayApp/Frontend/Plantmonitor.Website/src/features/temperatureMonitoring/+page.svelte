<script lang="ts">
    import {selectedDevice} from "../store";
    import {
        AutomaticPhotoTourClient,
        DashboardClient,
        MeasurementDevice,
        MeasurementStartInfo,
        PhotoTourInfo,
        RunningMeasurement,
        TemperatureClient,
        TemperatureMeasurement
    } from "~/services/GatewayAppApi";
    import {onDestroy, onMount} from "svelte";
    import {HubConnection, HubConnectionState} from "@microsoft/signalr";
    import {Task} from "~/types/Task";
    import * as echarts from "echarts";
    import TextInput from "../reuseableComponents/TextInput.svelte";
    import {DeviceStreaming} from "~/services/DeviceStreaming";

    let _connection: HubConnection | undefined;
    let _temperatureMeasurementById: Map<number, TemperatureMeasurement> = new Map();
    let _runningMeasurementByIp: Map<string, RunningMeasurement[]> = new Map();
    let _devices: MeasurementDevice[] = [];
    let _photoTours: PhotoTourInfo[] = [];
    const _liveTemperatureChartId = Math.random().toString(36).substring(7);
    let _liveChart: echarts.ECharts;
    let _liveTemperatureByDevice: Map<string, {date: Date; temperature: number}[]> = new Map();
    onDestroy(() => {
        _connection?.stop();
    });
    onMount(async () => {
        const photoTourClient = new AutomaticPhotoTourClient();
        _photoTours = await photoTourClient.getPhotoTours();
        await getMeasurements();
        _liveChart = echarts.init(document.getElementById(_liveTemperatureChartId));
        setTemperatureData([]);
    });
    async function showTemperaturesOfTour(info: PhotoTourInfo) {
        const dashboardClient = new DashboardClient();
        const summary = await dashboardClient.temperatureSummary(info.id);
        let newData: {name: string; type: string; showSymbol: boolean; data: (number | Date)[][]}[] = [];
        summary.forEach((x) => {
            newData.push({
                name: x.device,
                type: "line",
                showSymbol: true,
                data: x.data.filter((d) => d.temperature > 0).map((y) => [y.time, y.temperature])
            });
        });
        setTemperatureData(newData);
    }

    async function showTemperaturesOfMeasurement(measurement: TemperatureMeasurement) {
        const dashboardClient = new TemperatureClient();
        const temperatures = await dashboardClient.temperaturesOfMeasurement(measurement.id);
        let newData: {
            name: string;
            sampling: string;
            type: string;
            showSymbol: boolean;
            data: (number | Date)[][];
        }[] = [];
        newData.push({
            name: measurement.comment,
            type: "line",
            sampling: "lttb",
            showSymbol: true,
            data: temperatures.filter((t) => t.temperature > 0).map((y) => [y.timestamp, y.temperature])
        });
        setTemperatureData(newData);
    }

    function setTemperatureData(
        newData: {name: string; type: string; showSymbol: boolean; data: (number | Date)[][]}[],
        updateDataZoom = true
    ) {
        const xValues = newData.flatMap((d) => d.data.map((x) => x[0] as number));
        const getDataZoom = function () {
            if (!updateDataZoom) return [];
            const minX = Math.min(...xValues);
            const maxX = Math.max(...xValues);
            return [
                {
                    show: true,
                    realtime: true,
                    start: minX,
                    end: maxX,
                    xAxisIndex: [0, 1]
                },
                {
                    type: "inside",
                    realtime: true,
                    start: minX,
                    end: maxX,
                    xAxisIndex: [0, 1]
                }
            ];
        };
        _liveChart.clear();
        _liveChart.setOption({
            title: {text: "Temperatures: " + newData.map((x) => x.name).join(", ")},
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
            toolbox: {
                feature: {
                    dataZoom: {
                        yAxisIndex: "none"
                    },
                    restore: {},
                    saveAsImage: {}
                }
            },
            dataZoom: getDataZoom(),
            xAxis: {type: "time", name: "Time"},
            yAxis: {type: "value", name: "Temperature in °C"}
        });
        _liveChart.setOption({series: newData});
    }

    async function getMeasurements() {
        const temperatureClient = new TemperatureClient();
        _temperatureMeasurementById = (await temperatureClient.measurements()).toDictionary((x) => x.id);
        _runningMeasurementByIp = (await temperatureClient.getRunningMeasurements()).groupBy((x) => x.ip);
    }

    async function startMeasurement() {
        if ($selectedDevice == undefined) return;
        const temperatureClient = new TemperatureClient();
        await temperatureClient.addMeasurement(new MeasurementStartInfo({devices: _devices, ip: $selectedDevice.ip}));
        await Task.delay(3000);
        await getMeasurements();
    }

    async function getDevices() {
        if ($selectedDevice == undefined) return;
        const temperatureClient = new TemperatureClient();
        _devices = (await temperatureClient.getDevices($selectedDevice.ip)).map(
            (x) => new MeasurementDevice({sensorId: x, comment: ""})
        );
        await getMeasurements();
    }

    async function stopMeasurement(ip: string) {
        const temperatureClient = new TemperatureClient();
        await temperatureClient.stopMeasurement(ip);
        await getMeasurements();
    }
    async function stopLiveTemperatures() {
        if ($selectedDevice == undefined || _connection == undefined) return;
        _connection?.stop();
        _connection = undefined;
    }

    async function getTemperatures() {
        if ($selectedDevice == undefined) return;
        const temperatureClient = new TemperatureClient();
        const streamer = new DeviceStreaming();
        const devices = await temperatureClient.getDevices($selectedDevice.ip);
        const connection = streamer.temperatureConnection($selectedDevice.ip, devices);
        _connection = connection.connection;
        _liveChart.clear();

        await connection.start(async (temperature, device, date) => {
            if (_liveTemperatureByDevice.has(device)) {
                _liveTemperatureByDevice.get(device)?.push({date: date, temperature: temperature});
            } else _liveTemperatureByDevice.set(device, [{date: date, temperature: temperature}]);
            let newData: {name: string; type: string; showSymbol: boolean; data: (number | Date)[][]}[] = [];
            _liveTemperatureByDevice.forEach((value, key) => {
                newData.push({name: key, type: "line", showSymbol: true, data: value.map((x) => [x.date, x.temperature])});
            });
            setTemperatureData(newData, false);
        });
    }
</script>

<svelte:head><title>Temperature Monitoring</title></svelte:head>

<div class="col-md-12 row">
    <div style="height: 40vh;" id={_liveTemperatureChartId} class="col-md-12"></div>
    <div class="col-md-10"></div>
    {#if _connection == undefined || _connection?.state == HubConnectionState.Disconnected}
        <button
            disabled={$selectedDevice?.ip == undefined}
            on:click={getTemperatures}
            style="align-self: center;height: 40px;"
            class=" btn btn-primary col-md-2">Temperatures of {$selectedDevice?.ip}</button>
    {:else}
        <button on:click={() => stopLiveTemperatures()} style="align-self:center;height:40px" class="col-md-2 btn btn-danger"
            >Stop Live Temperatures</button>
    {/if}
    <hr class="mt-2" />
    <button on:click={getDevices} class="btn btn-primary col-md-2">Get Devices</button>
    {#each _devices as device}
        <TextInput class="col-md-2" label="Comment {device.sensorId}" bind:value={device.comment}></TextInput>
    {/each}
    <div class="col-md-12 mt-2"></div>
    <button
        disabled={_devices.length == 0 || _devices.filter((d) => d.comment.isEmpty()).length > 0}
        on:click={startMeasurement}
        class="btn btn-primary col-md-2">Start Measurement</button>
    <div class="col-md-12 row">
        {#each _runningMeasurementByIp.keys() as ip}
            <hr class="mt-2" />
            <div class="col-md-3">Running measurement: {ip}</div>
            {#each _runningMeasurementByIp.get(ip) ?? [] as measurement}
                <div class="col-md-1">
                    {_temperatureMeasurementById.get(measurement.measurementId)?.sensorId}: {_temperatureMeasurementById.get(
                        measurement.measurementId
                    )?.comment}
                </div>
            {/each}
            <button on:click={async () => await stopMeasurement(ip)} class="btn btn-danger col-md-2">Abort</button>
            <button class="btn btn-primary col-md-2 ms-2">Show data</button>
        {/each}
    </div>
    <hr />
    <div style="height: 30vh;overflow-y:auto" class="col-md-6 row">
        <h4>Existing Phototours</h4>
        {#each _photoTours.toSorted((a, b) => (a.lastEvent > b.lastEvent ? -1 : 1)) as tour}
            <div class="col-md-4">{tour.name}</div>
            <button on:click={() => showTemperaturesOfTour(tour)} class="btn btn-primary col-md-4">Show Temperatures</button>
            <div class="col-md-12 mt-2"></div>
        {/each}
    </div>
    <div style="height: 30vh;overflow-y:auto" class="col-md-6 row">
        <h4>All measurements</h4>
        {#each _temperatureMeasurementById.values() as measurement}
            <div class="col-md-4">{measurement.sensorId}</div>
            <div class="col-md-4">{measurement.comment}</div>
            <button on:click={() => showTemperaturesOfMeasurement(measurement)} class="col-md-4 btn btn-primary"
                >Show Temperatures</button>
            <div class="col-md-12 mt-2"></div>
        {/each}
    </div>
</div>
