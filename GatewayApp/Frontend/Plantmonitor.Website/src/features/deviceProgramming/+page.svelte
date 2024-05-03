<script lang="ts">
    "@hmr:keep-all";
    import {onDestroy, onMount} from "svelte";
    import {DeviceStreaming} from "~/services/DeviceStreaming";
    import {calculateMoveTo, stepsToReach} from "~/services/movementPointExtensions";
    import {
        DeviceClient,
        DeviceHealthState,
        DeviceMovement,
        MovementPoint,
        MovementProgrammingClient
    } from "~/services/GatewayAppApi";
    import NumberInput from "../reuseableComponents/NumberInput.svelte";
    import TextInput from "../reuseableComponents/TextInput.svelte";
    import type {HubConnection} from "@microsoft/signalr";
    import Checkbox from "../reuseableComponents/Checkbox.svelte";
    import {Task} from "~/types/task";
    import {dev} from "$app/environment";
    import {selectedDevice} from "../store";
    let videoCanvasId = crypto.randomUUID();
    let previewEnabled = false;
    let hubconnection: HubConnection | undefined;
    let selectedDevice2: DeviceHealthState | undefined;
    let moveSteps = 100;
    let currentlyMoving = false;
    let removeSteps = false;
    let storePictures = false;
    let currentPosition: number | undefined;
    let movementPlan = new DeviceMovement();
    let defaultFocus = 100;
    let newStep = new MovementPoint({focusInCentimeter: defaultFocus, speed: 200, stepOffset: 500, comment: ""});
    onMount(async () => {
        if ($selectedDevice != undefined) onDeviceSelected($selectedDevice);
    });
    const cancelSubscription = selectedDevice.subscribe((x) => {
        if (x == undefined) return;
        onDeviceSelected(x);
    });
    onDestroy(async () => {
        cancelSubscription();
        await hubconnection?.stop();
    });
    async function onDeviceSelected(device: DeviceHealthState) {
        if (previewEnabled) return;
        selectedDevice2 = device;
        const client = new MovementProgrammingClient();
        movementPlan = await client.getPlan(device.health?.deviceId);
        const newFocus = movementPlan?.movementPlan?.stepPoints.mean((x) => x.focusInCentimeter).roundTo(1);
        const deviceClient = new DeviceClient();
        currentPosition = await deviceClient.currentPosition(selectedDevice2.ip);
        defaultFocus = newFocus <= 0 ? defaultFocus : newFocus;
    }
    async function stopPreview() {
        if (selectedDevice2?.ip == undefined) return;
        await new DeviceClient().killCamera(selectedDevice2.ip);
        previewEnabled = false;
    }
    async function move(steps: number) {
        if (selectedDevice2?.ip == undefined) return;
        const client = new DeviceClient();
        await client.move(selectedDevice2.ip, steps, 500, 4000, 200);
        if (!previewEnabled) currentPosition = await client.currentPosition(selectedDevice2.ip);
    }
    async function zeroPosition() {
        if (selectedDevice2?.ip == undefined) return;
        const client = new DeviceClient();
        await client.zeroPosition(selectedDevice2.ip);
    }
    async function toggleMotorEngage(shouldBeEngaged: boolean) {
        if (selectedDevice2?.ip == undefined) return;
        const client = new DeviceClient();
        await client.toggleMotorEngage(selectedDevice2.ip, shouldBeEngaged);
    }
    async function updateSteps() {
        if (selectedDevice2?.ip == undefined) return;
        const client = new MovementProgrammingClient();
        movementPlan.deviceId = selectedDevice2.health.deviceId;
        movementPlan.movementPlanJson = "{}";
        await client.updatePlan(movementPlan);
    }
    async function moveTo(step: MovementPoint) {
        if (currentPosition == undefined) return;
        const stepsToMove = step[calculateMoveTo](movementPlan.movementPlan.stepPoints, currentPosition);
        currentlyMoving = true;
        await move(stepsToMove);
        currentlyMoving = false;
    }
    async function moveToAll() {
        if (currentPosition == undefined) return;
        currentlyMoving = true;
        for (let i = 0; i < movementPlan.movementPlan.stepPoints.length; i++) {
            const step = movementPlan.movementPlan.stepPoints[i];
            const stepsToMove = step[calculateMoveTo](movementPlan.movementPlan.stepPoints, currentPosition);
            await move(stepsToMove);
            const stepCountAfterMove = step[stepsToReach](movementPlan.movementPlan.stepPoints);
            while (currentPosition != stepCountAfterMove) await Task.delay(100);
        }
        currentlyMoving = false;
    }
    async function showPreview() {
        if (selectedDevice2?.ip == undefined) return;
        const connection = new DeviceStreaming().buildVideoConnection(
            selectedDevice2.ip,
            storePictures ? 1 : dev ? 8 : 4,
            defaultFocus / 100,
            storePictures
        );
        await hubconnection?.stop();
        hubconnection = connection.connection;
        connection.start(async (step, data) => {
            const image = document.getElementById(videoCanvasId) as HTMLImageElement;
            currentPosition = step;
            image.src = data;
        });
        previewEnabled = true;
    }
</script>

<div class="col-md-12 row">
    <div class="col-md-4 colm-2 row">
        <NumberInput class="col-md-4" label="Focus in cm" bind:value={defaultFocus}></NumberInput>
        <div class="col-md-5">Current Position: {currentPosition}</div>
        {#if previewEnabled}
            <button on:click={async () => await stopPreview()} class="btn btn-danger col-md-8">Stop Preview</button>
            <Checkbox disabledSelector={() => previewEnabled} class="col-md-4" label="Store Pictures" bind:value={storePictures}
            ></Checkbox>
            <NumberInput bind:value={moveSteps} label="Move Steps"></NumberInput>
            <button disabled={currentlyMoving} on:click={async () => await move(moveSteps)} class="btn btn-primary col-md-3"
                >Move</button>
            <button on:click={async () => await toggleMotorEngage(false)} class="btn btn-primary col-md-3"
                >Disengage Motor</button>
            <button on:click={async () => await toggleMotorEngage(true)} class="btn btn-primary col-md-3">Engage Motor</button>
            <button class="btn btn-dark col-md-3" on:click={async () => await zeroPosition()}>Zero Position</button>
        {:else}
            <button on:click={async () => await showPreview()} class="btn btn-primary col-md-8">Start Preview</button>
            <Checkbox class="col-md-4" label="Store Pictures" bind:value={storePictures}></Checkbox>
        {/if}
        <div style="height: 200px; overflow-y:scroll" class="col-md-12 row p-0">
            {#if movementPlan?.movementPlan?.stepPoints != undefined && movementPlan?.movementPlan?.stepPoints.length > 0}
                <div class="col-md-12 row">
                    <div class="col-md-11"></div>
                    <button on:click={async () => await moveToAll()} style="padding-left: 10px;" class="btn btn-primary col-md-1"
                        >All</button>
                </div>
                {#each movementPlan.movementPlan.stepPoints as step, i}
                    <div class="col-md-12 row mt-1 mb-1">
                        {#if removeSteps}
                            <button
                                on:click={() => {
                                    movementPlan.movementPlan.stepPoints.splice(i, 1);
                                    movementPlan = movementPlan;
                                }}
                                class="btn btn-danger col-md-1"
                                style="align-content: center;font-size:18px">X</button>
                        {:else}
                            <div style="align-content: center;font-size:18px" class="col-md-1"><b>{i + 1}</b></div>
                        {/if}
                        <NumberInput
                            disabledSelector={() => currentlyMoving}
                            class="col-md-3"
                            bind:value={step.stepOffset}
                            label="StepOffset"></NumberInput>
                        <NumberInput class="col-md-3" bind:value={step.focusInCentimeter} label="Focus"></NumberInput>
                        <TextInput class="col-md-4" bind:value={step.comment} label="Comment"></TextInput>
                        <button
                            disabled={currentlyMoving}
                            on:click={async () => await moveTo(step)}
                            style="font-size: 36px;"
                            class="btn btn-dark col-md-1 p-0 m-0">&#10149;</button>
                    </div>
                {/each}
            {:else}
                <h5>No Steps defined</h5>
            {/if}
        </div>
        <hr />
        <h4>New Step</h4>
        <div class="col-md-12 row colm-4">
            <NumberInput class="col-md-4" bind:value={newStep.stepOffset} label="StepOffset"></NumberInput>
            <NumberInput class="col-md-4" bind:value={newStep.focusInCentimeter} label="Focus"></NumberInput>
            <TextInput class="col-md-4" bind:value={newStep.comment} label="Comment"></TextInput>
            <button
                disabled={currentlyMoving}
                on:click={() => {
                    movementPlan.movementPlan.stepPoints.push(newStep.clone());
                    movementPlan = movementPlan;
                }}
                class="btn btn-primary">Add Step</button>
            <button disabled={currentlyMoving} on:click={() => (removeSteps = !removeSteps)} class="btn btn-danger"
                >Remove Steps</button>
            <TextInput class="col-md-6" bind:value={movementPlan.name} label="Movement Plan Name"></TextInput>
            <button class="btn btn-success" on:click={async () => await updateSteps()}>Save Steps</button>
        </div>
    </div>
    <div class="col-md-8 colm-3">
        <div style="height: 50vh;width:50vw;">
            <img style="height: 100%;width:100%" alt="preview" id={videoCanvasId} />
        </div>
    </div>
</div>
