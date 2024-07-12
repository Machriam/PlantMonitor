<script lang="ts">
    import {onDestroy, onMount} from "svelte";
    import {
        AutomaticPhotoTourClient,
        AutomaticTourStartInfo,
        DeviceMovement,
        MovementProgrammingClient,
        TemperatureClient,
        TemperatureMeasurementInfo,
        type AutomaticPhotoTour
    } from "~/services/GatewayAppApi";
    import TextInput from "../reuseableComponents/TextInput.svelte";
    import NumberInput from "../reuseableComponents/NumberInput.svelte";
    import {selectedDevice, allDevices} from "../store";
    import {isValid} from "./AutomaticTourStartInfoExtensions";
    import {calculateMoveTo} from "~/services/movementPointExtensions";
    onDestroy(() => {});
    let runningTours: AutomaticPhotoTour[] = [];
    let movementPlan: DeviceMovement | undefined;
    let startInfo: AutomaticTourStartInfo = new AutomaticTourStartInfo();
    let availableSensors: {ip: string; guid: string; name: string; sensors: string[]}[] = [];
    selectedDevice.subscribe(async (x) => {
        if (x?.health.deviceId == undefined || $selectedDevice?.health.deviceId == undefined) return;
        startInfo.deviceGuid = $selectedDevice?.health.deviceId;
        const movementClient = new MovementProgrammingClient();
        movementPlan = await movementClient.getPlan(x?.health.deviceId);
        if (movementPlan.movementPlan.stepPoints.length > 0) startInfo.movementPlan = movementPlan.id;
        else startInfo.movementPlan = 0;
    });
    onMount(async () => {
        const photoTourClient = new AutomaticPhotoTourClient();
        const temperatureClient = new TemperatureClient();
        startInfo.temperatureMeasureDevice = [];
        for (let i = 0; i < $allDevices.length; i++) {
            const device = $allDevices[i];
            if (device.health?.deviceName == undefined || device.health?.deviceId == undefined) continue;
            const name = device.health.deviceName;
            const guid = device.health.deviceId;
            temperatureClient.getDevices(device.ip).then((sensors) => {
                availableSensors.push({ip: device.ip, guid: guid, name: name, sensors: sensors});
                availableSensors = availableSensors;
            });
        }
        runningTours = await photoTourClient.getRunningPhotoTours();
    });
    function AddSensors(guid: string, comment: string) {
        const existingSensor = startInfo.temperatureMeasureDevice.find((x) => x.guid == guid);
        if (existingSensor == undefined) {
            startInfo.temperatureMeasureDevice.push(new TemperatureMeasurementInfo({comment: comment, guid: guid}));
            startInfo.temperatureMeasureDevice = startInfo.temperatureMeasureDevice;
            return;
        }
        startInfo.temperatureMeasureDevice = startInfo.temperatureMeasureDevice.filter((x) => x.guid != guid);
        startInfo.temperatureMeasureDevice = startInfo.temperatureMeasureDevice;
    }
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
        <button on:click={AddPhotoTour} disabled={!startInfo[isValid]($selectedDevice)} class="btn btn-primary col-md-2"
            >Add new Tour</button>
        <hr class="col-md-12 mt-2" />
        <h4 class="col-md-12">Avalailable Sensors</h4>
        <div class="col-md-12 row">
            {#each availableSensors as sensor}
                <div class="col-md-2 me-1">
                    <button
                        on:click={() => AddSensors(sensor.guid, sensor.name)}
                        class="card-body card {startInfo.temperatureMeasureDevice?.filter((x) => x.guid == sensor.guid).length > 0
                            ? 'bg-opacity-25 bg-info'
                            : ''}"
                        style="text-align: center">
                        {sensor.name}<br />
                        <div class="col-md-12 d-flex flex-row justify-content-center">
                            {#if sensor.sensors.length === 0}
                                <span class=""> No sensors </span>
                            {/if}
                            {#each sensor.sensors as s}
                                <span class="me-1"> {s} </span>
                            {/each}
                        </div>
                    </button>
                </div>
            {/each}
        </div>
        <div class="col-md-4 d-flex flex-column card mt-2">
            <h4>Movement Plan</h4>
            {#if movementPlan !== undefined}
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
