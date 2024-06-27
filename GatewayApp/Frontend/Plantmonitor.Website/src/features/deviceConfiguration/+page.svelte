<script lang="ts">
    "@hmr:keep-all";
    import {onDestroy, onMount} from "svelte";
    import {
        AppConfigurationClient,
        AssociatePowerOutletModel,
        CameraType,
        DeviceClient,
        DeviceConfigurationClient,
        DeviceHealthState,
        HealthState,
        OutletModel,
        PowerOutletClient,
        WebSshCredentials,
        type IAssociatePowerOutletModel
    } from "../../services/GatewayAppApi";
    import {Task} from "~/types/task";
    import {CvInterop, ThermalImage} from "./CvInterop";
    import TextInput from "../reuseableComponents/TextInput.svelte";
    import PasswordInput from "../reuseableComponents/PasswordInput.svelte";
    import PictureStreamer from "../deviceProgramming/PictureStreamer.svelte";
    import Select from "../reuseableComponents/Select.svelte";

    let configurationData: {ipFrom: string; ipTo: string; userName: string; userPassword: string} = {
        ipFrom: "",
        ipTo: "",
        userName: "",
        userPassword: ""
    };
    let configurationClient: DeviceConfigurationClient;
    let devices: DeviceHealthState[] = [];
    let outletByDevice: {[key: string]: AssociatePowerOutletModel | null} = {};
    let previewImage = new ThermalImage();
    let pictureStreamer: PictureStreamer;
    let existingOutlets: OutletModel[] = [];
    let selectedOutlet: OutletModel | undefined;
    let logData = "";
    let allOutletsFetched = false;

    let searchingForDevices = true;
    let webSshLink = ""; // @hmr:keep
    function healthStateFormatter(state: HealthState) {
        const result = Object.getOwnPropertyNames(HealthState)
            .filter((x) => !parseInt(x) && x != "0")
            .filter((x) => (HealthState as unknown as {[key: string]: number})[x] & state);
        if (result.length == 0) return [HealthState[HealthState.NA]];
        return result;
    }
    let webSshCredentials: WebSshCredentials;
    onMount(async () => {
        configurationClient = new DeviceConfigurationClient();
        const outletClient = new PowerOutletClient();
        existingOutlets = await outletClient.getOutlets();
        existingOutlets.sort((a, b) => (a.name + a.channel + a.buttonNumber).localeCompare(b.name + b.channel + b.buttonNumber));
        webSshCredentials = await configurationClient.getWebSshCredentials();
        await getDeviceStatus();
        searchingForDevices = false;
    });
    onDestroy(() => {
        pictureStreamer?.stopStreaming();
    });
    async function openConsole(ip: string | undefined): Promise<void> {
        if (ip == undefined) return;
        webSshLink = "";
        await Task.delay(100);
        webSshLink = `${webSshCredentials.protocol}://${location.hostname}:${webSshCredentials.port}/?hostname=${ip}&username=${webSshCredentials.user}&password=${webSshCredentials.password?.asBase64()}`;
    }
    async function configureDevice(ip: string | undefined): Promise<void> {
        if (ip == undefined) return;
        webSshLink = "";
        await Task.delay(100);
        var certificate = await configurationClient.getCertificateData();
        const command =
            `sudo mkdir /srv/certs/;echo '${certificate.certificate}' | sudo tee /srv/certs/plantmonitor.crt;echo '${certificate.key}' | sudo tee /srv/certs/plantmonitor.key;` +
            `sudo echo -e "set -g mouse on\\nset -g history-limit 4096" > ~/.tmux.conf;` +
            `sudo apt-get update;sudo apt-get install -y tmux;tmux new '` +
            `sudo apt-get install -y git;` +
            `git clone https://github.com/Machriam/PlantMonitor.git;cd PlantMonitor;git reset --hard;git pull; sudo chmod -R 755 *;cd PlantMonitorControl/Install;./install.sh;` +
            `exec bash;'`;
        webSshLink = `${webSshCredentials.protocol}://${location.hostname}:${webSshCredentials.port}/?hostname=${ip}&username=${webSshCredentials.user}&password=${webSshCredentials.password?.asBase64()}&command=${command.urlEncoded()}`;
    }
    async function showPreviewImage(ip: string | undefined) {
        if (ip == undefined) return;
        const deviceClient = new DeviceClient();
        await deviceClient.killCamera(ip, CameraType.Vis);
        previewImage = {dataUrl: await (await deviceClient.previewImage(ip, CameraType.Vis)).data.asBase64Url()};
    }
    async function calibrateExposure(ip: string | undefined) {
        if (ip == undefined) return;
        const client = new DeviceClient();
        client.calibrateExposure(ip);
    }
    async function runFFC(ip: string | undefined) {
        if (ip == undefined) return;
        const client = new DeviceClient();
        client.runFFC(ip);
    }
    async function getLogData(ip: string | undefined) {
        if (ip == undefined) return;
        const configurationClient = new DeviceConfigurationClient();
        logData = await configurationClient.getDeviceLog(ip);
    }
    async function showThermalImage(ip: string | undefined) {
        if (ip == undefined) return;
        const deviceClient = new DeviceClient();
        await deviceClient.killCamera(ip, CameraType.IR);
        const cvInterop = new CvInterop();
        const data = (await deviceClient.previewImage(ip, CameraType.IR)).data;
        previewImage = cvInterop.thermalDataToImage(new Uint32Array(await data.arrayBuffer()));
    }
    async function testMovement(ip: string | undefined) {
        if (ip == undefined) return;
        const deviceClient = new DeviceClient();
        await deviceClient.move(ip, -1500, 1000, 10000, 300);
    }
    async function showThermalVideo(ip: string | undefined) {
        if (ip == undefined) return;
        pictureStreamer.stopStreaming();
        pictureStreamer.showPreview(ip, CameraType.IR, 1);
    }
    async function showTestVideo(ip: string | undefined) {
        if (ip == undefined) return;
        pictureStreamer.stopStreaming();
        pictureStreamer.showPreview(ip, CameraType.Vis, 1);
    }
    async function checkStatus(ip: string) {
        const deviceClient = new DeviceConfigurationClient();
        await deviceClient.recheckDevice(ip);
    }
    async function updateIpRange() {
        const client = new AppConfigurationClient();
        client.updateIpRanges(configurationData.ipFrom, configurationData.ipTo);
        configurationData.ipFrom = "";
        configurationData.ipTo = "";
    }
    async function updateDeviceSettings() {
        const client = new AppConfigurationClient();
        await client.updateDeviceSettings(configurationData.userPassword, configurationData.userName);
        configurationClient = new DeviceConfigurationClient();
        webSshCredentials = await configurationClient.getWebSshCredentials();
        configurationData.userName = "";
        configurationData.userPassword = "";
    }
    async function switchPowerOutlet(code: number | undefined) {
        if (code == undefined) return;
        const switchDevices = devices.filter((d) => d.health.state != undefined && d.health.state & HealthState.CanSwitchOutlets);
        const outletClient = new PowerOutletClient();
        for (let i = 0; i < switchDevices.length; i++) {
            await outletClient.disablePrompts().switchOutlet(switchDevices[i].ip, code).try();
            await Task.delay(200);
        }
    }
    async function getDeviceStatus() {
        const client = new DeviceConfigurationClient();
        const outletClient = new PowerOutletClient();
        try {
            devices = await client.getDevices();
            const outletDevices = devices.filter((d) => d.health?.deviceId != undefined);
            for (let i = 0; i < outletDevices.length; i++) {
                const deviceId = outletDevices[i].health.deviceId!;
                const {result, error, hasError} = await outletClient.powerOutletForDevice(deviceId).try();
                if (hasError) {
                    console.log(error);
                    outletByDevice[deviceId] = null;
                    continue;
                }
                outletByDevice[deviceId] = result;
            }
            allOutletsFetched = true;
        } catch (ex) {
            console.log(ex);
            devices = [];
        }
    }
    async function switchOutlet(model: OutletModel | undefined, deviceId: string) {
        const outletClient = new PowerOutletClient();
        const data: IAssociatePowerOutletModel = {
            deviceId: deviceId,
            switchOnId: model?.switchOnId,
            switchOffId: model?.switchOffId
        };
        outletClient.associateDeviceWithPowerOutlet(new AssociatePowerOutletModel(data));
        await getDeviceStatus();
    }
</script>

<div class="col-md-12 row">
    <h3>Plantmonitor Configuration</h3>
    <div class="col-md-12 row">
        <TextInput class="col-md-2" bind:value={configurationData.ipFrom} label="IP From"></TextInput>
        <TextInput class="col-md-2" bind:value={configurationData.ipTo} label="IP To"></TextInput>
        <button on:click={updateIpRange} class="btn btn-primary col-md-2">Update IP</button>
        <div class="col-md-12 mt-2"></div>
        <TextInput class="col-md-2" bind:value={configurationData.userName} label="Device SSH User"></TextInput>
        <PasswordInput class="col-md-2" bind:value={configurationData.userPassword} label="Device SSH Password"></PasswordInput>
        <button on:click={updateDeviceSettings} class="btn btn-primary col-md-2">Update Password</button>
    </div>
    <h3>
        {#if searchingForDevices}
            Searching for devices
        {:else}
            Found devices:
        {/if}
    </h3>
    <div class="col-md-6">
        {#each devices as device}
            <table class="table">
                <thead> <tr> <th>IP</th> <th>Action</th> </tr> </thead>
                <tbody>
                    <tr>
                        <td class="col-md-3">
                            {#if device.health != undefined}
                                <span class="badge bg-success">{device.ip}</span><br />
                                <span>{device.health.deviceName}</span><br />
                                <span>{device.health.deviceId}</span><br />
                                <span>
                                    {#each healthStateFormatter(device.health.state ?? HealthState.NA) as state}
                                        <span>{state}<br /></span>
                                    {/each}
                                </span>
                            {:else}
                                <span class="badge bg-danger">{device.ip}</span>
                            {/if}
                        </td>
                        <td>
                            <button on:click={() => configureDevice(device.ip)} class="btn btn-primary"> Configure </button>
                            {#if device.health != undefined}
                                <button on:click={() => showPreviewImage(device.ip)} class="btn btn-primary">
                                    Preview Image
                                </button>
                                <button on:click={() => showTestVideo(device.ip)} class="btn btn-primary"> Preview Video </button>
                                <button on:click={() => testMovement(device.ip)} class="btn btn-primary"> Test Movement </button>
                                <button on:click={() => showThermalImage(device.ip)} class="btn btn-primary">
                                    Preview IR Image
                                </button>
                                <button on:click={() => showThermalVideo(device.ip)} class="btn btn-primary">
                                    Preview IR Video
                                </button>
                                <button on:click={() => getLogData(device.ip)} class="btn btn-primary"> Show Logs </button>
                                <button on:click={() => runFFC(device.ip)} class="btn btn-primary"> FFC</button>
                                <button on:click={() => calibrateExposure(device.ip)} class="btn btn-primary"> Update Exposure</button>
                                {#if device.health.deviceId != undefined && allOutletsFetched}
                                    <div style="align-items: center;" class="col-form-label col-md-12 row ps-3">
                                        Associated Outlet:
                                        <Select
                                            class="col-md-8"
                                            initialSelectedItem={outletByDevice[device.health.deviceId]?.switchOnId?.toString()}
                                            selectedItemChanged={(x) => switchOutlet(x, device.health.deviceId ?? "")}
                                            textSelector={(x) => `${x.name} Channel ${x.channel} Button ${x.buttonNumber}`}
                                            idSelector={(x) => x.switchOnId.toString()}
                                            items={existingOutlets}></Select>
                                    </div>
                                {/if}
                                <button class="btn btn-primary" on:click={async () => await checkStatus(device.ip)}
                                    >Check Device</button>
                            {/if}
                            <button on:click={() => openConsole(device.ip)} class="btn btn-primary"> Open Console </button>
                        </td>
                    </tr>
                </tbody>
            </table>
        {/each}
        <button on:click={async () => await getDeviceStatus()} class="btn btn-primary">Update</button>
        <Select
            selectedItemChanged={(x) => (selectedOutlet = x)}
            textSelector={(x) => `${x.name} Channel: ${x.channel} Button: ${x.buttonNumber}`}
            items={existingOutlets}
            class="col-md-6"></Select>
        {#if selectedOutlet != undefined}
            <button on:click={async () => await switchPowerOutlet(selectedOutlet?.switchOnId)} class="btn btn-success"
                >Power On</button>
            <button on:click={async () => await switchPowerOutlet(selectedOutlet?.switchOffId)} class="btn btn-danger"
                >Power Off</button>
        {/if}
    </div>
    <div class="col-md-6">
        {#if !previewImage.dataUrl?.isEmpty()}
            <img alt="preview" src={previewImage.dataUrl} />
        {/if}
        <PictureStreamer bind:this={pictureStreamer}></PictureStreamer>
        <div class="col-md-12" style="height:80vh;">
            {#if !webSshLink.isEmpty()}
                <iframe style="height: 100%;width:100%" title="Web SSH" src={webSshLink}></iframe>
            {/if}
            {#if !logData.isEmpty()}
                <textarea style="height: 100%; width:100%">{logData.split("\n").toReversed().join("\n")}</textarea>
            {/if}
        </div>
        <div style="height: 20vh;"></div>
    </div>
</div>
