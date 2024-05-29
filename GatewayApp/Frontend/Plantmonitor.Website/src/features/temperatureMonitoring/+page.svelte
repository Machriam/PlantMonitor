<script lang="ts">
    import {DeviceStreaming} from "~/services/DeviceStreaming";
    import {selectedDevice} from "../store";
    import {
        MeasurementDevice,
        MeasurementStartInfo,
        TemperatureClient,
        TemperatureMeasurement,
        type IMeasurementStartInfo
    } from "~/services/GatewayAppApi";
    import {onDestroy} from "svelte";
    import {HubConnection} from "@microsoft/signalr";
    let _connection: HubConnection | undefined;
    let _temperatureMeasurements: TemperatureMeasurement[] = [];
    let _devices: MeasurementDevice[] = [];
    onDestroy(() => {});
    async function getMeasurements() {
        const temperatureClient = new TemperatureClient();
        _temperatureMeasurements = await temperatureClient.measurements();
    }
    async function startMeasurement() {
        if ($selectedDevice == undefined || $selectedDevice == null) return;
        const temperatureClient = new TemperatureClient();
        temperatureClient.addMeasurement(new MeasurementStartInfo({devices: _devices, ip: $selectedDevice.ip}));
    }
    async function getDevices() {
        if ($selectedDevice == undefined || $selectedDevice == null) return;
        const temperatureClient = new TemperatureClient();
        _devices = (await temperatureClient.getDevices($selectedDevice.ip)).map(
            (x) => new MeasurementDevice({deviceId: x, comment: ""})
        );
    }
    async function getTemperatures() {
        if ($selectedDevice == undefined || $selectedDevice == null) return;
        const temperatureClient = new TemperatureClient();
        const streamer = new DeviceStreaming();
        const devices = await temperatureClient.getDevices($selectedDevice.ip);
        const connection = streamer.temperatureConnection($selectedDevice.ip, devices);
        _connection = connection.connection;
        connection.start(async (temperature, device, date) => {
            console.log(device, temperature, date);
        });
    }
</script>

<div class="col-md-12 row">
    <button on:click={getTemperatures} class=" btn btn-primary">Show Temperatures</button>
    <div>{$selectedDevice?.ip}</div>
    <button on:click={getDevices} class="btn btn-primary">Get Devices</button>
    <div class="col-md-12">
        {#each _devices as device}
            <div>{device.deviceId}</div>
            <input type="text" class="form-control" bind:value={device.comment} />
        {/each}
    </div>
    <button on:click={startMeasurement} class="btn btn-primary">Start Measurement</button>
</div>
