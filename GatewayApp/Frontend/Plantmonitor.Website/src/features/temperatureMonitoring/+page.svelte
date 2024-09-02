<script lang="ts">
    import {DeviceStreaming} from "~/services/DeviceStreaming";
    import {selectedDevice} from "../store";
    import {
        MeasurementDevice,
        MeasurementStartInfo,
        RunningMeasurement,
        TemperatureClient,
        TemperatureMeasurement
    } from "~/services/GatewayAppApi";
    import {onDestroy, onMount} from "svelte";
    import {HubConnection} from "@microsoft/signalr";
    import {Task} from "~/types/Task";
    import * as echarts from "echarts";

    let _connection: HubConnection | undefined;
    let _temperatureMeasurementById: Map<number, TemperatureMeasurement> = new Map();
    let _runningMeasurementByIp: Map<string, RunningMeasurement[]> = new Map();
    let _devices: MeasurementDevice[] = [];
    const _databaseTemperatureChartId = Math.random().toString(36).substring(7);
    const _liveTemperatureChartId = Math.random().toString(36).substring(7);
    let _liveChart: echarts.ECharts;
    let _liveTemperatureByDevice: Map<string, {date: Date; temperature: number}[]> = new Map();
    onDestroy(() => {
        _connection?.stop();
    });
    onMount(async () => {
        await getMeasurements();
        _liveChart = echarts.init(document.getElementById(_liveTemperatureChartId));
    });

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

    async function getTemperatures() {
        if ($selectedDevice == undefined) return;
        const temperatureClient = new TemperatureClient();
        const streamer = new DeviceStreaming();
        const devices = await temperatureClient.getDevices($selectedDevice.ip);
        const connection = streamer.temperatureConnection($selectedDevice.ip, devices);
        _connection = connection.connection;
        _liveChart.clear();
        _liveChart.setOption({
            title: {text: "Live Temperature"},
            series: [],
            tooltip: {
                trigger: "axis",
                axisPointer: {animation: false},
                formatter: function (params: {seriesName: string; value: [Date, number]}[]) {
                    return params.map((x) => x.seriesName + ": " + x.value[1].toFixed(1) + "°C").join("<br>");
                }
            },
            xAxis: {type: "time"},
            yAxis: {type: "value"}
        });
        await connection.start(async (temperature, device, date) => {
            if (_liveTemperatureByDevice.has(device)) {
                _liveTemperatureByDevice.get(device)?.push({date: date, temperature: temperature});
            } else _liveTemperatureByDevice.set(device, [{date: date, temperature: temperature}]);
            let newData: {name: string; type: string; showSymbol: boolean; data: (number | Date)[][]}[] = [];
            _liveTemperatureByDevice.forEach((value, key) => {
                newData.push({name: key, type: "line", showSymbol: true, data: value.map((x) => [x.date, x.temperature])});
            });
            _liveChart.setOption({
                series: newData
            });
        });
    }
</script>

<svelte:head><title>Temperature Monitoring</title></svelte:head>

<div class="col-md-12 row">
    <button disabled={$selectedDevice?.ip == undefined} on:click={getTemperatures} class=" btn btn-primary col-md-2"
        >Show Temperatures</button>
    <div style="height: 40vh;" id={_liveTemperatureChartId} class="col-md-10"></div>
    <div>{$selectedDevice?.ip}</div>
    <hr />
    <button on:click={getDevices} class="btn btn-primary col-md-2">Get Devices</button>
    <div class="col-md-12">
        {#each _devices as device}
            <div>{device.sensorId}</div>
            <input type="text" class="form-control" bind:value={device.comment} />
        {/each}
    </div>
    <button on:click={startMeasurement} class="btn btn-primary col-md-2">Start Measurement</button>
    <div class="col-md-12 row">
        {#each _runningMeasurementByIp.keys() as ip}
            <div class="col-md-1">{ip}</div>
            {#each _runningMeasurementByIp.get(ip) ?? [] as measurement}
                <div class="col-md-1">{_temperatureMeasurementById.get(measurement.measurementId)?.comment}</div>
            {/each}
            <button on:click={async () => await stopMeasurement(ip)} class="btn btn-danger col-md-2">Abort</button>
        {/each}
    </div>
</div>
<div id={_databaseTemperatureChartId}></div>
