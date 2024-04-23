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
        MovementPoint,
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
    let moveSteps = 100;
    let currentPosition: number | undefined;
    let movementPlan = new DeviceMovement();
    let defaultFocus = 100;
    let newStep = new MovementPoint({focusInCentimeter: defaultFocus, speed: 200, stepOffset: 500, comment: ""});
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
        const newFocus = movementPlan?.movementPlan?.stepPoints.mean((x) => x.focusInCentimeter).roundTo(1);
        defaultFocus = newFocus <= 0 ? defaultFocus : newFocus;
    }
    async function stopPreview() {
        if (selectedDevice?.ip == undefined) return;
        await new DeviceClient().killCamera(selectedDevice.ip);
        previewEnabled = false;
    }
    async function move(steps: number) {
        if (selectedDevice?.ip == undefined) return;
        const client = new DeviceClient();
        await client.move(selectedDevice.ip, steps, 500, 4000, 200);
        currentPosition = await client.currentPosition(selectedDevice.ip);
    }
    async function zeroPosition() {
        if (selectedDevice?.ip == undefined) return;
        const client = new DeviceClient();
        await client.zeroPosition(selectedDevice.ip);
        currentPosition = await client.currentPosition(selectedDevice.ip);
    }
    async function toggleMotorEngage(shouldBeEngaged: boolean) {
        if (selectedDevice?.ip == undefined) return;
        const client = new DeviceClient();
        await client.toggleMotorEngage(selectedDevice.ip, shouldBeEngaged);
    }
    async function updateSteps() {
        if (selectedDevice?.ip == undefined) return;
        const client = new MovementProgrammingClient();
        movementPlan.deviceId = selectedDevice.health.deviceId;
        movementPlan.movementPlanJson = "{}";
        await client.updatePlan(movementPlan);
    }
    async function showPreview() {
        if (selectedDevice?.ip == undefined) return;
        const connection = new DeviceStreaming().buildVideoConnection(selectedDevice.ip, 4, defaultFocus);
        await hubconnection?.stop();
        hubconnection = connection.connection;
        connection.start(async (data) => {
            const image = document.getElementById(videoCanvasId) as HTMLImageElement;
            image.src = data;
        });
        const client = new DeviceClient();
        currentPosition = await client.currentPosition(selectedDevice.ip);
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
    <div class="col-md-4 colm-2">
        <NumberInput label="Focus in cm" bind:value={defaultFocus}></NumberInput>
        {#if previewEnabled}
            <div>Current Position: {currentPosition}</div>
            <button on:click={async () => await stopPreview()} class="btn btn-danger">Stop Preview</button>
            <NumberInput bind:value={moveSteps} label="Move Steps"></NumberInput>
            <button on:click={async () => await move(moveSteps)} class="btn btn-primary">Move</button>
            <button on:click={async () => await toggleMotorEngage(false)} class="btn btn-primary">Disengage Motor</button>
            <button on:click={async () => await toggleMotorEngage(true)} class="btn btn-primary">Engage Motor</button>
            <button class="btn btn-dark" on:click={async () => await zeroPosition()}>Zero Position</button>
        {:else}
            <button on:click={async () => await showPreview()} class="btn btn-primary">Start Preview</button>
        {/if}
        {#if movementPlan?.movementPlan?.stepPoints != undefined && movementPlan?.movementPlan?.stepPoints.length > 0}
            {#each movementPlan.movementPlan.stepPoints as step}
                <div class="col-md-12 row mt-1 mb-1">
                    <NumberInput class="col-md-4" bind:value={step.stepOffset} label="StepOffset"></NumberInput>
                    <NumberInput class="col-md-4" bind:value={step.focusInCentimeter} label="Focus"></NumberInput>
                    <TextInput class="col-md-4" bind:value={step.comment} label="Comment"></TextInput>
                </div>
            {/each}
        {:else}
            <h5>No Steps defined</h5>
        {/if}
        <hr />
        <h4>New Step</h4>
        <div class="col-md-12 row colm-4">
            <NumberInput class="col-md-4" bind:value={newStep.stepOffset} label="StepOffset"></NumberInput>
            <NumberInput class="col-md-4" bind:value={newStep.focusInCentimeter} label="Focus"></NumberInput>
            <TextInput class="col-md-4" bind:value={newStep.comment} label="Comment"></TextInput>
            <button
                on:click={() => {
                    movementPlan.movementPlan.stepPoints.push(newStep);
                    movementPlan = movementPlan;
                }}
                class="btn btn-primary">Add Step</button>
            <TextInput class="col-md-6" bind:value={movementPlan.name} label="Movement Plan Name"></TextInput>
            <button class="btn btn-success" on:click={async () => await updateSteps()}>Save Steps</button>
        </div>
    </div>
    <div class="col-md-8">
        <img style="height: 50vh;width:50vw" alt="preview" id={videoCanvasId} />
    </div>
</div>
