<script lang="ts">
    "@hmr:keep-all";
    import {onDestroy, onMount} from "svelte";
    import {DeviceStreaming} from "~/services/DeviceStreaming";
    import {
        DeviceClient,
        DeviceConfigurationClient,
        DeviceHealthState,
        DeviceMovement,
        MovementPlan,
        MovementProgrammingClient
    } from "~/services/GatewayAppApi";
    import NumberInput from "../reuseableComponents/NumberInput.svelte";
    import TextInput from "../reuseableComponents/TextInput.svelte";
    import type {HubConnection} from "@microsoft/signalr";
    let videoCanvasId = crypto.randomUUID();
    let previewEnabled = false;
    let devices: DeviceHealthState[] = [];
    let hubconnection: HubConnection | undefined;
    let selectedDevice: DeviceHealthState | undefined;
    //@ts-ignore
    let movementPlan: DeviceMovement = new DeviceMovement({movementPlan: {focusInCentimeter: 50, stepPoints: [], speed: 100}});
    onMount(async () => {
        const client = new DeviceConfigurationClient();
        devices = await client.getDevices();
    });
    onDestroy(async () => {
        await hubconnection?.stop();
    });
    async function onDeviceSelected(device: DeviceHealthState) {
        if (previewEnabled) return;
        selectedDevice = device;
        const client = new MovementProgrammingClient();
        movementPlan = await client.getPlan(device.health?.deviceId);
        movementPlan.movementPlan.focusInCentimeter = 50;
    }
    async function stopPreview() {
        if (selectedDevice?.ip == undefined) return;
        await new DeviceClient().killCamera(selectedDevice.ip);
        previewEnabled = false;
    }
    async function move() {
        if (selectedDevice?.ip == undefined) return;
        const client = new DeviceClient();
        await client.move(selectedDevice.ip, 100, 500, 4000, 200);
    }
    async function showPreview() {
        if (selectedDevice?.ip == undefined) return;
        const connection = new DeviceStreaming().buildVideoConnection(
            selectedDevice.ip,
            4,
            movementPlan.movementPlan?.focusInCentimeter / 100
        );
        await hubconnection?.stop();
        hubconnection = connection.connection;
        connection.start(async (data) => {
            const image = document.getElementById(videoCanvasId) as HTMLImageElement;
            image.src = data;
        });
        previewEnabled = true;
    }
</script>

<div class="col-md-12 row">
    <h3>Program Devices</h3>
    <div class="col-md-12 d-flex flex-row">
        {#each devices as device}
            <button on:click={() => onDeviceSelected(device)} class="alert {selectedDevice?.ip == device.ip ? 'alert-info' : ''}">
                {device.health?.deviceName}<br />
                {device.ip}
            </button>
        {/each}
    </div>
    <div class="col-md-4">
        <NumberInput label="Focus in cm" bind:value={movementPlan.movementPlan.focusInCentimeter}></NumberInput>
        {#if previewEnabled}
            <button on:click={async () => await stopPreview()} class="btn btn-danger">Stop Preview</button>
        {:else}
            <button on:click={async () => await showPreview()} class="btn btn-primary">Start Preview</button>
        {/if}
    </div>
    <div class="col-md-8">
        <img style="height: 50vh;width:50vw" alt="preview" id={videoCanvasId} />
    </div>
</div>
