<script lang="ts">
    interface IEventMap {
        select: DeviceHealthState | undefined;
    }
    import {onDestroy, onMount} from "svelte";
    import {DeviceConfigurationClient, DeviceHealthState} from "~/services/GatewayAppApi";
    import {createEventDispatcher} from "svelte";
    let selectedDevice: DeviceHealthState | undefined;
    let intervalId: number | undefined;
    export let refreshTimeInSeconds: number;

    const dispatch = createEventDispatcher<IEventMap>();
    let devices: DeviceHealthState[] = [];
    function deviceSelected(device: DeviceHealthState | undefined) {
        selectedDevice = device;
        dispatch("select", device);
    }
    onMount(async () => {
        intervalId = setInterval(async () => {
            const healthClient = new DeviceConfigurationClient();
            devices = await healthClient.getDevices();
            if (selectedDevice != undefined && !devices.map((d) => d.ip).includes(selectedDevice.ip)) deviceSelected(undefined);
        }, 1000 * refreshTimeInSeconds);
    });
    onDestroy(() => {
        clearInterval(intervalId);
    });
</script>

<div class="d-flex flex-row" {...$$restProps}>
    {#each devices as device}
        <button
            on:click={() => deviceSelected(device)}
            class="p-0 m-0 ms-3 alert {(selectedDevice?.ip == device.ip ? 'selected' : '') + ` available-${device.retryTimes}`}">
            {device.health?.deviceName}<br />
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
</style>
