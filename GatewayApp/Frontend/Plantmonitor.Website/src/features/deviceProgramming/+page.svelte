<script lang="ts">
    "@hmr:keep-all";
    import {onDestroy, onMount} from "svelte";
    import {calculateMoveTo, stepsToReach} from "~/services/movementPointExtensions";
    import {
        CameraType,
        DeviceClient,
        DeviceHealthState,
        DeviceMovement,
        MotorPosition,
        MovementPoint,
        MovementProgrammingClient
    } from "~/services/GatewayAppApi";
    import NumberInput from "../reuseableComponents/NumberInput.svelte";
    import TextInput from "../reuseableComponents/TextInput.svelte";
    import {Task} from "~/types/task";
    import {selectedDevice} from "../store";
    import PictureStreamer from "./PictureStreamer.svelte";
    let previewEnabled = false;
    let visStreamer: PictureStreamer;
    let irStreamer: PictureStreamer;
    let selectedDeviceData: DeviceHealthState | undefined;
    let moveSteps = 100;
    let currentlyMoving = false;
    let removeSteps = false;
    let currentPosition: MotorPosition | undefined;
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
    onDestroy(() => {
        cancelSubscription();
        irStreamer?.stopStreaming();
        visStreamer?.stopStreaming();
    });
    async function onDeviceSelected(device: DeviceHealthState) {
        if (previewEnabled) return;
        selectedDeviceData = device;
        const client = new MovementProgrammingClient();
        movementPlan = await client.getPlan(device.health?.deviceId);
        const newFocus = movementPlan?.movementPlan?.stepPoints.mean((x) => x.focusInCentimeter).roundTo(1);
        const deviceClient = new DeviceClient();
        currentPosition = await deviceClient.currentPosition(selectedDeviceData.ip);
        defaultFocus = newFocus <= 0 ? defaultFocus : newFocus;
    }
    async function stopPreview() {
        if (selectedDeviceData?.ip == undefined) return;
        await irStreamer?.stopStreaming();
        await visStreamer?.stopStreaming();
        previewEnabled = false;
    }
    async function move(steps: number) {
        if (selectedDeviceData?.ip == undefined) return false;
        const client = new DeviceClient();
        const result = await client.move(selectedDeviceData.ip, steps, 500, 4000, 200).try();
        if (result.hasError) return false;
        currentPosition = await client.currentPosition(selectedDeviceData.ip);
        return true;
    }
    async function zeroPosition() {
        if (selectedDeviceData?.ip == undefined) return;
        const client = new DeviceClient();
        await client.zeroPosition(selectedDeviceData.ip);
        currentPosition = await client.currentPosition(selectedDeviceData.ip);
    }
    async function toggleMotorEngage(shouldBeEngaged: boolean) {
        if (selectedDeviceData?.ip == undefined) return;
        const client = new DeviceClient();
        await client.toggleMotorEngage(selectedDeviceData.ip, shouldBeEngaged);
        currentPosition = await client.currentPosition(selectedDeviceData.ip);
    }
    async function updateSteps() {
        if (selectedDeviceData?.ip == undefined || selectedDeviceData.health.deviceId == undefined) return;
        const client = new MovementProgrammingClient();
        movementPlan.deviceId = selectedDeviceData.health.deviceId;
        movementPlan.movementPlanJson = "{}";
        await client.updatePlan(movementPlan);
    }
    async function moveTo(step: MovementPoint) {
        if (currentPosition?.position == undefined) return;
        const stepsToMove = step[calculateMoveTo](movementPlan.movementPlan.stepPoints, currentPosition.position);
        currentlyMoving = true;
        await move(stepsToMove);
        currentlyMoving = false;
    }
    async function moveToAll() {
        if (currentPosition?.position == undefined || selectedDeviceData == undefined) return;
        currentlyMoving = true;
        for (let i = 0; i < movementPlan.movementPlan.stepPoints.length; i++) {
            const step = movementPlan.movementPlan.stepPoints[i];
            const stepsToMove = step[calculateMoveTo](movementPlan.movementPlan.stepPoints, currentPosition.position);
            const moveable = await move(stepsToMove);
            if (!moveable) {
                currentlyMoving = false;
                return;
            }
            const stepCountAfterMove = step[stepsToReach](movementPlan.movementPlan.stepPoints);
            while (stepCountAfterMove != visStreamer.currentPosition || stepCountAfterMove != irStreamer.currentPosition) {
                currentPosition.position = irStreamer.currentPosition;
                await Task.delay(100);
            }
            currentPosition.position = stepCountAfterMove;
            await Task.delay(1000);
        }
        currentlyMoving = false;
    }
    async function takePhotoTrip() {
        if (selectedDeviceData?.ip == undefined) return;
        let positionsToReach = movementPlan.movementPlan.stepPoints.map((sp) =>
            sp[stepsToReach](movementPlan.movementPlan.stepPoints)
        );
        visStreamer.storeDataStream(positionsToReach, selectedDeviceData.ip, CameraType.Vis, defaultFocus);
        irStreamer.storeDataStream(positionsToReach, selectedDeviceData.ip, CameraType.IR, defaultFocus);
        previewEnabled = true;
        while (!visStreamer.firstDataReceived || !irStreamer.firstDataReceived) await Task.delay(100);
        await moveToAll();
        const pictureClient = new DeviceClient();
        await pictureClient.killCamera(selectedDeviceData?.ip, CameraType.Vis);
        await pictureClient.killCamera(selectedDeviceData?.ip, CameraType.IR);
    }
    async function showPreview() {
        if (selectedDeviceData?.ip == undefined) return;
        visStreamer.showPreview(selectedDeviceData.ip, CameraType.Vis, defaultFocus);
        irStreamer.showPreview(selectedDeviceData.ip, CameraType.IR, defaultFocus);
        previewEnabled = true;
    }
</script>

<div class="col-md-12 row">
    <div class="col-md-4 colm-2 row">
        <NumberInput class="col-md-4" label="Focus in cm" bind:value={defaultFocus}></NumberInput>
        <div class="col-md-8">
            <div>Pos: {currentPosition?.position}</div>
            <div>Engaged: {currentPosition?.engaged}</div>
        </div>
        {#if previewEnabled}
            <button on:click={async () => await stopPreview()} class="btn btn-danger col-md-8">Stop Preview</button>
            <NumberInput bind:value={moveSteps} label="Move Steps"></NumberInput>
            <button disabled={currentlyMoving} on:click={async () => await move(moveSteps)} class="btn btn-primary col-md-3"
                >Move</button>
            <button on:click={async () => await toggleMotorEngage(false)} class="btn btn-primary col-md-3"
                >Disengage Motor</button>
            <button on:click={async () => await toggleMotorEngage(true)} class="btn btn-primary col-md-3">Engage Motor</button>
            <button class="btn btn-dark col-md-3" on:click={async () => await zeroPosition()}>Zero Position</button>
        {:else}
            <button on:click={async () => await showPreview()} class="btn btn-primary col-md-4">Start Preview</button>
            <div class="col-md-4"></div>
            <button on:click={async () => await takePhotoTrip()} class="btn btn-success col-md-4">Store Photo Trip</button>
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
            <PictureStreamer bind:this={visStreamer}></PictureStreamer>
            <PictureStreamer bind:this={irStreamer}></PictureStreamer>
        </div>
    </div>
</div>
