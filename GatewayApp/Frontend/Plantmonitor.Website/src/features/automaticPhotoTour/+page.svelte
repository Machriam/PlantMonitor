<script lang="ts">
    import {onDestroy, onMount} from "svelte";
    import {
        AutomaticPhotoTourClient,
        AutomaticTourStartInfo,
        DeviceMovement,
        MovementPlan,
        MovementProgrammingClient,
        TemperatureClient,
        type AutomaticPhotoTour
    } from "~/services/GatewayAppApi";
    import TextInput from "../reuseableComponents/TextInput.svelte";
    import NumberInput from "../reuseableComponents/NumberInput.svelte";
    import {selectedDevice, allDevices} from "../store";
    import {isValid} from "./AutomaticTourStartInfoExtensions";
    import {calculateMoveTo, stepsToReach} from "~/services/movementPointExtensions";
    onDestroy(() => {});
    let runningTours: AutomaticPhotoTour[] = [];
    let movementPlan: DeviceMovement | undefined;
    let startInfo: AutomaticTourStartInfo = new AutomaticTourStartInfo();
    let availableSensors: {ip: string; name: string; sensors: string[]}[] = [];
    selectedDevice.subscribe(async (x) => {
        if (x?.health.deviceId == undefined) return;
        var movementClient = new MovementProgrammingClient();
        movementPlan = await movementClient.getPlan(x?.health.deviceId);
        if (movementPlan.movementPlan.stepPoints.length > 0) startInfo.movementPlan = movementPlan.id;
        else startInfo.movementPlan = 0;
    });
    onMount(async () => {
        const photoTourClient = new AutomaticPhotoTourClient();
        const temperatureClient = new TemperatureClient();
        for (let i = 0; i < $allDevices.length; i++) {
            const device = $allDevices[i];
            if (device.health?.deviceName == undefined) continue;
            const sensors = await temperatureClient.getDevices(device.ip);
            availableSensors.push({ip: device.ip, name: device.health.deviceName, sensors: sensors});
        }
        runningTours = await photoTourClient.getRunningPhotoTours();
    });
    async function AddPhotoTour() {
        const photoTourClient = new AutomaticPhotoTourClient();
        await photoTourClient.startAutomaticTour(startInfo);
    }
</script>

<div class="col-md-12 row">
    <div class="col-md-12 row">
        <div>Running Tours:</div>
        {#each runningTours as tour}
            <div class="ms-3">{tour.name}</div>
        {/each}
    </div>
    <div class="col-md-12 row">
        <TextInput class="col-md-2" bind:value={startInfo.name} label="Name"></TextInput>
        <TextInput class="col-md-2" bind:value={startInfo.comment} label="Comment"></TextInput>
        <NumberInput class="col-md-2" bind:value={startInfo.intervallInMinutes} step={0.1} label="Interval in min"></NumberInput>
        <button disabled={!startInfo[isValid]($selectedDevice)} class="btn btn-primary col-md-2">Add new Tour</button>
        <div class="col-md-12"></div>
        <div class="col-md-4">
            {#each availableSensors as sensor}
                {sensor.ip}
                {#each sensor.sensors as s}
                    {s}
                {/each}
            {/each}
        </div>
        <div class="col-md-4 d-flex flex-column">
            {#if movementPlan != undefined}
                {#each movementPlan.movementPlan.stepPoints as point, index}
                    <div class="row col-md-12">
                        <span class="col-md-3"> Step {index + 1}: </span>
                        <span class="col-md-3"> Pos {point[calculateMoveTo](movementPlan.movementPlan.stepPoints, 0)} </span>
                        <span class="col-md-6">{point.comment}</span>
                    </div>
                {/each}
            {/if}
        </div>
    </div>
</div>
