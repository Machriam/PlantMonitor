<script lang="ts">
    import {onDestroy, onMount} from "svelte";
    import {
        AutomaticPhotoTourClient,
        AutomaticTourStartInfo,
        DeviceMovement,
        MovementProgrammingClient,
        TemperatureClient,
        TemperatureMeasurementInfo,
        type AutomaticPhotoTour,
        PhotoTourInfo,
        PhotoTourEvent,
        PhotoTourEventType
    } from "~/services/GatewayAppApi";
    import TextInput from "../reuseableComponents/TextInput.svelte";
    import NumberInput from "../reuseableComponents/NumberInput.svelte";
    import {selectedDevice, allDevices} from "../store";
    import {isValid} from "./AutomaticTourStartInfoExtensions";
    import {calculateMoveTo} from "~/services/movementPointExtensions";
    import Checkbox from "../reuseableComponents/Checkbox.svelte";

    onDestroy(() => {});
    let movementPlan: DeviceMovement | undefined;
    let startInfo: AutomaticTourStartInfo = new AutomaticTourStartInfo();
    let existingPhototours: PhotoTourInfo[] = [];
    let availableSensors: {ip: string; guid: string; name: string; sensors: string[]}[] = [];
    let selectedEvents: PhotoTourEvent[] = [];
    let selectedPhotoTour: PhotoTourInfo | undefined;
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
        existingPhototours = await photoTourClient.getPhotoTours();
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
    });

    function AddSensors(guid: string, comment: string) {
        const existingSensor = startInfo.temperatureMeasureDevice.find((x) => x.guid == guid);
        if (existingSensor === undefined) {
            startInfo.temperatureMeasureDevice.push(new TemperatureMeasurementInfo({comment: comment, guid: guid}));
            startInfo.temperatureMeasureDevice = startInfo.temperatureMeasureDevice;
            return;
        }
        startInfo.temperatureMeasureDevice = startInfo.temperatureMeasureDevice.filter((x) => x.guid != guid);
        startInfo.temperatureMeasureDevice = startInfo.temperatureMeasureDevice;
    }

    async function GetEvents(photoTourId: number) {
        const photoTourClient = new AutomaticPhotoTourClient();
        selectedEvents = await photoTourClient.getEvents(photoTourId, false);
        selectedPhotoTour = existingPhototours.find((ep) => ep.id == photoTourId);
    }

    async function PausePhotoTour() {
        if (selectedPhotoTour == undefined) return;
        const photoTourClient = new AutomaticPhotoTourClient();
        selectedPhotoTour.finished = !selectedPhotoTour.finished;
        const result = await photoTourClient.pausePhotoTour(selectedPhotoTour.id, selectedPhotoTour.finished).try();
        if (result.hasError) selectedPhotoTour.finished = !selectedPhotoTour.finished;
        existingPhototours = existingPhototours;
    }

    async function AddPhotoTour() {
        const photoTourClient = new AutomaticPhotoTourClient();
        await photoTourClient.startAutomaticTour(startInfo);
    }
</script>

<div class="col-md-12 row">
    <div class="col-md-12 row">
        <TextInput class="col-md-2" bind:value={startInfo.name} label="Name"></TextInput>
        <TextInput class="col-md-2" bind:value={startInfo.comment} label="Comment"></TextInput>
        <NumberInput class="col-md-2" bind:value={startInfo.intervallInMinutes} step={0.1} label="Interval in min"></NumberInput>
        <Checkbox class="col-md-2 align-content-center" label="Use IR?" bind:value={startInfo.shouldUseIR}></Checkbox>
        <button on:click={AddPhotoTour} disabled={!startInfo[isValid]($selectedDevice)} class="btn btn-primary col-md-2"
            >Add new Tour
        </button>
        <hr class="col-md-12 mt-2" />
        <h4 class="col-md-12">Available Sensors</h4>
        <div class="col-md-12 row">
            {#each availableSensors as sensor}
                <div class="col-md-2 me-1">
                    <button
                        on:click={() => AddSensors(sensor.guid, sensor.name)}
                        class="card-body card {startInfo.temperatureMeasureDevice?.filter((x) => x.guid === sensor.guid).length >
                        0
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
        <div class="col-md-12 row">
            <h4>Previous Phototours</h4>
            {#each existingPhototours as tour}
                <div class="col-md-2">
                    <button
                        on:click={async () => await GetEvents(tour.id)}
                        class="alert {tour === selectedPhotoTour ? 'alert-info' : ''}">
                        <div>{tour.name}</div>
                        <div>Finished: {tour.finished}</div>
                        <div>{tour.firstEvent.toLocaleTimeString()} {tour.firstEvent.toDateString()}</div>
                        <div>{tour.firstEvent.toLocaleTimeString()} {tour.lastEvent.toDateString()}</div>
                    </button>
                    {#if selectedPhotoTour !== undefined && tour === selectedPhotoTour}
                        <div class="col-md-12 form-check form-switch">
                            <label class="form-check-label">Stopped?</label>
                            <input
                                on:click={async () => await PausePhotoTour()}
                                type="checkbox"
                                bind:checked={selectedPhotoTour.finished}
                                class="form-check-input" />
                        </div>
                    {/if}
                </div>
            {/each}
        </div>
        <div class="col-md-12">
            <table class="table">
                <thead>
                    <tr>
                        <th>Type</th>
                        <th>Time</th>
                        <th>Message</th>
                    </tr>
                </thead>
                <tbody>
                    {#each selectedEvents as event}
                        <tr>
                            <td>{PhotoTourEventType[event.type]}</td>
                            <td>{event.timestamp.toLocaleTimeString()}</td>
                            <td>{event.message}</td>
                        </tr>
                    {/each}
                </tbody>
            </table>
        </div>
    </div>
</div>
