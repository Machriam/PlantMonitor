<script lang="ts">
    import {DeviceStreaming} from "~/services/DeviceStreaming";
    import {selectedDevice} from "../store";
    import {TemperatureClient} from "~/services/GatewayAppApi";
    import {onDestroy} from "svelte";
    import {HubConnection} from "@microsoft/signalr";
    let _connection: HubConnection | undefined;
    onDestroy(() => {});
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
</div>
