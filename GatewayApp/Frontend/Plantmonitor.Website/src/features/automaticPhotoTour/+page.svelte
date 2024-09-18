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
    import {pipe} from "~/types/Pipe";

    onDestroy(() => {});
    let _movementPlan: DeviceMovement | undefined;
    let _startInfo: AutomaticTourStartInfo = new AutomaticTourStartInfo();
    let _existingPhototours: PhotoTourInfo[] = [];
    let _availableSensors: {ip: string; guid: string; name: string; sensors: string[]}[] = [];
    let _selectedEvents: PhotoTourEvent[] = [];
    let _selectedPhotoTour: PhotoTourInfo | undefined;
    selectedDevice.subscribe(async (x) => {
        if (x?.health.deviceId == undefined || $selectedDevice?.health.deviceId == undefined) return;
        _startInfo.deviceGuid = $selectedDevice?.health.deviceId;
        const movementClient = new MovementProgrammingClient();
        _movementPlan = await movementClient.getPlan(x?.health.deviceId);
        if (_movementPlan.movementPlan.stepPoints.length > 0) _startInfo.movementPlan = _movementPlan.id;
        else _startInfo.movementPlan = 0;
    });
    onMount(async () => {
        const photoTourClient = new AutomaticPhotoTourClient();
        const temperatureClient = new TemperatureClient();
        _existingPhototours = await photoTourClient.getPhotoTours();
        _startInfo.temperatureMeasureDevice = [];
        for (let i = 0; i < $allDevices.length; i++) {
            const device = $allDevices[i];
            if (device.health?.deviceName == undefined || device.health?.deviceId == undefined) continue;
            const name = device.health.deviceName;
            const guid = device.health.deviceId;
            temperatureClient.getDevices(device.ip).then((sensors) => {
                _availableSensors.push({ip: device.ip, guid: guid, name: name, sensors: sensors});
                _availableSensors = _availableSensors;
            });
        }
    });

    function AddSensors(guid: string, comment: string) {
        const existingSensor = _startInfo.temperatureMeasureDevice.find((x) => x.guid == guid);
        if (existingSensor === undefined) {
            _startInfo.temperatureMeasureDevice.push(new TemperatureMeasurementInfo({comment: comment, guid: guid}));
            _startInfo.temperatureMeasureDevice = _startInfo.temperatureMeasureDevice;
            return;
        }
        _startInfo.temperatureMeasureDevice = _startInfo.temperatureMeasureDevice.filter((x) => x.guid != guid);
        _startInfo.temperatureMeasureDevice = _startInfo.temperatureMeasureDevice;
    }

    async function GetEvents(photoTourId: number) {
        const photoTourClient = new AutomaticPhotoTourClient();
        _selectedEvents = await photoTourClient.getEvents(photoTourId, false);
        _selectedPhotoTour = _existingPhototours.find((ep) => ep.id == photoTourId);
        _startInfo.intervallInMinutes = _selectedPhotoTour?.intervallInMinutes ?? 0;
        _startInfo.name = _selectedPhotoTour?.name ?? "";
        _startInfo.comment = _selectedPhotoTour?.comment ?? "";
        _startInfo.pixelSizeInMm = _selectedPhotoTour?.pixelSizeInMm ?? 0;
    }

    async function UpdatePhotoTour() {
        if (_selectedPhotoTour == undefined) return;
        const photoTourClient = new AutomaticPhotoTourClient();
        await photoTourClient.updatePhotoTour(_selectedPhotoTour.id, _startInfo.intervallInMinutes, _startInfo.pixelSizeInMm);
        _existingPhototours = await photoTourClient.getPhotoTours();
    }

    async function PausePhotoTour() {
        if (_selectedPhotoTour == undefined) return;
        const photoTourClient = new AutomaticPhotoTourClient();
        _selectedPhotoTour.finished = !_selectedPhotoTour.finished;
        const result = await pipe(photoTourClient.pausePhotoTour(_selectedPhotoTour.id, _selectedPhotoTour.finished)).try();
        if (result.hasError) _selectedPhotoTour.finished = !_selectedPhotoTour.finished;
        _existingPhototours = _existingPhototours;
    }

    async function AddPhotoTour() {
        const photoTourClient = new AutomaticPhotoTourClient();
        await photoTourClient.startAutomaticTour(_startInfo);
        _existingPhototours = await photoTourClient.getPhotoTours();
    }
</script>

<svelte:head><title>Automatic Photo Tour</title></svelte:head>

<div class="col-md-12 row">
    <div class="col-md-12 row">
        <TextInput class="col-md-2" bind:value={_startInfo.name} label="Name"></TextInput>
        <TextInput class="col-md-2" bind:value={_startInfo.comment} label="Comment"></TextInput>
        <NumberInput class="col-md-1" bind:value={_startInfo.intervallInMinutes} step={0.1} label="Interval in min"></NumberInput>
        <NumberInput class="col-md-1" bind:value={_startInfo.pixelSizeInMm} step={0.1} label="mm per Pixel"></NumberInput>
        <Checkbox class="col-md-2 align-content-center" label="Use IR?" bind:value={_startInfo.shouldUseIR}></Checkbox>
        <button
            on:click={AddPhotoTour}
            disabled={!_startInfo[isValid]($selectedDevice)}
            style="height: 40px;align-self:center"
            class="btn btn-primary col-md-2"
            >Add new Tour
        </button>
        <button
            on:click={UpdatePhotoTour}
            disabled={!_startInfo[isValid]($selectedDevice)}
            style="height: 40px;align-self:center"
            class="btn btn-primary col-md-2 ms-2"
            >Update Tour Parameter
        </button>
        <hr class="col-md-12 mt-2" />
        <h4 class="col-md-12">Available Sensors</h4>
        <div class="col-md-12 row">
            {#each _availableSensors as sensor}
                <div class="col-md-2 me-1">
                    <button
                        on:click={() => AddSensors(sensor.guid, sensor.name)}
                        class="card-body card {_startInfo.temperatureMeasureDevice?.filter((x) => x.guid === sensor.guid).length >
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
            {#if _movementPlan !== undefined}
                {#each _movementPlan.movementPlan.stepPoints as point, index}
                    <div class="row col-md-12">
                        <span class="col-md-3"> Step {index + 1}: </span>
                        <span class="col-md-3"> Pos {point[calculateMoveTo](_movementPlan.movementPlan.stepPoints, 0)} </span>
                        <span class="col-md-6">{point.comment}</span>
                    </div>
                {/each}
            {/if}
        </div>
        <div class="col-md-12 mt-2">
            <h4>Previous Phototours</h4>
            <div style="overflow-x:auto; width=80vw" class="d-flex flex-row col-md-12">
                {#each _existingPhototours.toSorted( (a, b) => (a.finished && !b.finished ? 1 : b.finished && !a.finished ? -1 : a.lastEvent <= b.lastEvent ? 1 : -1) ) as tour}
                    <div class="col-md-2">
                        <button
                            on:click={async () => await GetEvents(tour.id)}
                            class="alert {tour === _selectedPhotoTour ? 'alert-info' : ''}">
                            <div>{tour.name}</div>
                            <div>Finished: {tour.finished}</div>
                            <div>{tour.firstEvent.toLocaleTimeString()} {tour.firstEvent.toDateString()}</div>
                            <div>{tour.firstEvent.toLocaleTimeString()} {tour.lastEvent.toDateString()}</div>
                        </button>
                        {#if _selectedPhotoTour !== undefined && tour === _selectedPhotoTour}
                            <div class="col-md-12 form-check form-switch">
                                <div class="form-check-label">Stopped?</div>
                                <input
                                    on:click={async () => await PausePhotoTour()}
                                    type="checkbox"
                                    bind:checked={_selectedPhotoTour.finished}
                                    class="form-check-input" />
                            </div>
                        {/if}
                    </div>
                {/each}
            </div>
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
                    {#each _selectedEvents as event}
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
