<script lang="ts">
    interface IEventMap {
        select: DeviceHealthState | undefined;
        allDevices: DeviceHealthState[];
    }
    import {onDestroy, onMount} from "svelte";
    import {DeviceConfigurationClient, DeviceHealthState} from "~/services/GatewayAppApi";
    import {createEventDispatcher} from "svelte";
    import {pipe} from "~/types/Pipe";
    let _selectedDevice: DeviceHealthState | undefined;
    let _intervalId: number | undefined;
    export let _refreshTimeInSeconds: number;

    const dispatch = createEventDispatcher<IEventMap>();
    let devices: DeviceHealthState[] = [];
    function deviceSelected(device: DeviceHealthState | undefined) {
        _selectedDevice = device;
        dispatch("select", device);
    }
    onMount(async () => {
        _intervalId = setInterval(async () => {
            const healthClient = new DeviceConfigurationClient();
            devices = await healthClient.getDevices();
            dispatch("allDevices", devices);
            if (_selectedDevice != undefined && !devices.map((d) => d.ip).includes(_selectedDevice.ip)) deviceSelected(undefined);
        }, 1000 * _refreshTimeInSeconds);
    });
    onDestroy(() => {
        clearInterval(_intervalId);
    });
</script>

<div class="d-flex flex-row" {...$$restProps}>
    {#each devices as device}
        <button
            on:click={() => deviceSelected(device)}
            class="p-0 m-0 ms-3 alert {(_selectedDevice?.health?.deviceId == device.health?.deviceId ? 'selected' : '') +
                ` available-${pipe(device.retryTimes).limit(0, 5)}`}">
            {device.health?.deviceName ?? "not configured"}<br />
            {device.ip}
        </button>
    {/each}
</div>

<style>
    .selected {
        font-weight: bold;
    }
    .available-0 {
        background-color: #7de04c;
    }
    .available-1 {
        background-color: #9af053;
    }
    .available-2 {
        background-color: #a4eb4d;
    }
    .available-3 {
        background-color: #c4fa55;
    }
    .available-4 {
        background-color: #dbf457;
    }
    .available-5 {
        background-color: #cacbc5;
    }
</style>
