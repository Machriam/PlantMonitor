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

    let _connection: HubConnection | undefined;
    let _temperatureMeasurementById: Map<number, TemperatureMeasurement> = new Map();
    let _runningMeasurementByIp: Map<string, RunningMeasurement[]> = new Map();
    let _devices: MeasurementDevice[] = [];
    onDestroy(() => {
        _connection?.stop();
    });
    onMount(async () => {
        await getMeasurements();
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
        await connection.start(async (temperature, device, date) => {
            console.log(device, temperature, date);
        });
    }
</script>

<svelte:head><title>Temperature Monitoring</title></svelte:head>

<div class="col-md-12 row">
    <button on:click={getTemperatures} class=" btn btn-primary">Show Temperatures</button>
    <div>{$selectedDevice?.ip}</div>
    <button on:click={getDevices} class="btn btn-primary">Get Devices</button>
    <div class="col-md-12">
        {#each _devices as device}
            <div>{device.sensorId}</div>
            <input type="text" class="form-control" bind:value={device.comment} />
        {/each}
    </div>
    <button on:click={startMeasurement} class="btn btn-primary">Start Measurement</button>
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
