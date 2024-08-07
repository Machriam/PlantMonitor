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
    } from "~/services/GatewayAppApi";
    import {Task} from "~/types/task";
    import {CvInterop, ThermalImage} from "./CvInterop";
    import TextInput from "../reuseableComponents/TextInput.svelte";
    import PasswordInput from "../reuseableComponents/PasswordInput.svelte";
    import PictureStreamer from "../deviceProgramming/PictureStreamer.svelte";
    import Select from "../reuseableComponents/Select.svelte";
    import {formatHealthState} from "~/services/healthStateExtensions";

    let configurationData: {ipFrom: string; ipTo: string; userName: string; userPassword: string} = {
        ipFrom: "",
        ipTo: "",
        userName: "",
        userPassword: ""
    };
    let _configurationClient: DeviceConfigurationClient;
    let _devices: DeviceHealthState[] = [];
    let _outletByDevice: {[key: string]: AssociatePowerOutletModel | null} = {};
    let _previewImage = new ThermalImage();
    let _pictureStreamer: PictureStreamer;
    let _existingOutlets: OutletModel[] = [];
    let _selectedOutlet: OutletModel | undefined;
    let _logData = "";
    let _allSeenDevices: DeviceHealthState[] = [];
    let _allOutletsFetched = false;

    let _searchingForDevices = true;
    let _webSshLink = ""; // @hmr:keep
    let _webSshCredentials: WebSshCredentials;
    onMount(async () => {
        _configurationClient = new DeviceConfigurationClient();
        const outletClient = new PowerOutletClient();
        _existingOutlets = await outletClient.getOutlets();
        _existingOutlets.sort((a, b) => (a.name + a.channel + a.buttonNumber).localeCompare(b.name + b.channel + b.buttonNumber));
        _webSshCredentials = await _configurationClient.getWebSshCredentials();
        await getDeviceStatus();
        _searchingForDevices = false;
    });
    onDestroy(() => {
        _pictureStreamer?.stopStreaming();
    });
    async function openConsole(ip: string | undefined): Promise<void> {
        if (ip == undefined) return;
        _webSshLink = "";
        await Task.delay(100);
        _webSshLink = `${_webSshCredentials.protocol}://${location.hostname}:${_webSshCredentials.port}/?hostname=${ip}&username=${_webSshCredentials.user}&password=${_webSshCredentials.password?.asBase64()}`;
    }
    async function configureDevice(ip: string | undefined): Promise<void> {
        if (ip == undefined) return;
        _webSshLink = "";
        await Task.delay(100);
        const certificate = await _configurationClient.getCertificateData();
        const command =
            `sudo mkdir /srv/certs/;echo '${certificate.certificate}' | sudo tee /srv/certs/plantmonitor.crt;echo '${certificate.key}' | sudo tee /srv/certs/plantmonitor.key;` +
            `sudo echo -e "set -g mouse on\\n set -g history-limit 4096" > ~/.tmux.conf;` +
            `sudo apt-get update;sudo apt-get install -y tmux;tmux new '` +
            `sudo apt-get install -y git;` +
            `git clone https://github.com/Machriam/PlantMonitor.git;cd PlantMonitor;git reset --hard;git pull; sudo chmod -R 755 *;cd PlantMonitorControl/Install;./install.sh;` +
            `exec bash;'`;
        _webSshLink = `${_webSshCredentials.protocol}://${location.hostname}:${_webSshCredentials.port}/?hostname=${ip}&username=${_webSshCredentials.user}&password=${_webSshCredentials.password?.asBase64()}&command=${command.urlEncoded()}`;
    }
    async function showPreviewImage(ip: string | undefined) {
        if (ip == undefined) return;
        const deviceClient = new DeviceClient();
        await deviceClient.killCamera(ip, CameraType.Vis);
        _previewImage = {dataUrl: await (await deviceClient.previewImage(ip, CameraType.Vis)).data.asBase64Url()};
    }
    async function calibrateExposure(ip: string | undefined) {
        if (ip == undefined) return;
        const client = new DeviceClient();
        await client.calibrateExposure(ip);
    }
    async function runFFC(ip: string | undefined) {
        if (ip == undefined) return;
        const client = new DeviceClient();
        await client.runFFC(ip);
    }
    async function getLogData(ip: string | undefined) {
        if (ip == undefined) return;
        const configurationClient = new DeviceConfigurationClient();
        _logData = await configurationClient.getDeviceLog(ip);
    }
    async function showThermalImage(ip: string | undefined) {
        if (ip == undefined) return;
        const deviceClient = new DeviceClient();
        await deviceClient.killCamera(ip, CameraType.IR);
        const cvInterop = new CvInterop();
        const data = (await deviceClient.previewImage(ip, CameraType.IR)).data;
        _previewImage = cvInterop.thermalDataToImage(new Uint32Array(await data.arrayBuffer()));
    }
    async function testMovement(ip: string | undefined) {
        if (ip == undefined) return;
        const deviceClient = new DeviceClient();
        await deviceClient.move(ip, -1500, 1000, 10000, 300);
    }
    async function showThermalVideo(ip: string | undefined) {
        if (ip == undefined) return;
        _pictureStreamer.stopStreaming();
        _pictureStreamer.showPreview(ip, CameraType.IR, 1);
    }
    async function showTestVideo(ip: string | undefined) {
        if (ip == undefined) return;
        _pictureStreamer.stopStreaming();
        _pictureStreamer.showPreview(ip, CameraType.Vis, 1);
    }
    async function checkStatus(ip: string) {
        const deviceClient = new DeviceConfigurationClient();
        const newHealth = await deviceClient.recheckDevice(ip);
        const checkedDevice = _devices.find((d) => d.ip == ip);
        if (checkedDevice == undefined) return;
        checkedDevice.health = newHealth;
        _devices = _devices;
    }
    async function updateIpRange() {
        const client = new AppConfigurationClient();
        await client.updateIpRanges(configurationData.ipFrom, configurationData.ipTo);
        configurationData.ipFrom = "";
        configurationData.ipTo = "";
    }
    async function updateDeviceSettings() {
        const client = new AppConfigurationClient();
        await client.updateDeviceSettings(configurationData.userPassword, configurationData.userName);
        _configurationClient = new DeviceConfigurationClient();
        _webSshCredentials = await _configurationClient.getWebSshCredentials();
        configurationData.userName = "";
        configurationData.userPassword = "";
    }
    async function switchPowerOutlet(code: number | undefined) {
        if (code == undefined) return;
        const switchDevices = _devices.filter(
            (d) => d.health != undefined && d.health.state != undefined && d.health.state & HealthState.CanSwitchOutlets
        );
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
            _devices = await client.getDevices();
            const outletDevices = _devices.filter((d) => d.health?.deviceId != undefined);
            for (let i = 0; i < outletDevices.length; i++) {
                const deviceId = outletDevices[i].health.deviceId!;
                const {result, error, hasError} = await outletClient.powerOutletForDevice(deviceId).try();
                if (hasError) {
                    console.log(error);
                    _outletByDevice[deviceId] = null;
                    continue;
                }
                _outletByDevice[deviceId] = result;
            }
            _allOutletsFetched = true;
        } catch (ex) {
            console.log(ex);
            _devices = [];
        }
    }
    async function switchOutlet(model: OutletModel | undefined, deviceId: string) {
        const outletClient = new PowerOutletClient();
        const data: IAssociatePowerOutletModel = {
            deviceId: deviceId,
            switchOnId: model?.switchOnId,
            switchOffId: model?.switchOffId
        };
        await outletClient.associateDeviceWithPowerOutlet(new AssociatePowerOutletModel(data));
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
        {#if _searchingForDevices}
            Searching for devices
        {:else}
            Found devices:
        {/if}
    </h3>
    <div class="col-md-6">
        {#each _devices as device}
            <table class="table">
                <thead> <tr> <th>IP</th> <th>Action</th> </tr> </thead>
                <tbody>
                    <tr>
                        <td class="col-md-3">
                            {#if device.health !== undefined}
                                <span class="badge bg-success">{device.ip}</span><br />
                                <span>{device.health.deviceName}</span><br />
                                <span>{device.health.deviceId}</span><br />
                                <span>
                                    {#each formatHealthState(device.health.state ?? HealthState.NA) as state}
                                        <span>{state}<br /></span>
                                    {/each}
                                </span>
                            {:else}
                                <span class="badge bg-danger">{device.ip}</span>
                            {/if}
                        </td>
                        <td>
                            <button on:click={() => configureDevice(device.ip)} class="btn btn-primary"> Configure </button>
                            {#if device.health !== undefined}
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
                                <button on:click={() => calibrateExposure(device.ip)} class="btn btn-primary">
                                    Update Exposure</button>
                                {#if device.health.deviceId !== undefined && _allOutletsFetched}
                                    <div style="align-items: center;" class="col-form-label col-md-12 row ps-3">
                                        Associated Outlet:
                                        <Select
                                            class="col-md-8"
                                            initialSelectedItem={_outletByDevice[device.health.deviceId]?.switchOnId?.toString()}
                                            selectedItemChanged={(x) => switchOutlet(x, device.health.deviceId ?? "")}
                                            textSelector={(x) => `${x.name} Channel ${x.channel} Button ${x.buttonNumber}`}
                                            idSelector={(x) => x.switchOnId.toString()}
                                            items={_existingOutlets}></Select>
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
        {#each _allSeenDevices as seenDevice}
            {#if _devices.filter((d) => d.ip == seenDevice.ip).length == 0}
                <div></div>
            {/if}
        {/each}
        <button on:click={async () => await getDeviceStatus()} class="btn btn-primary">Update</button>
        <Select
            selectedItemChanged={(x) => (_selectedOutlet = x)}
            textSelector={(x) => `${x.name} Channel: ${x.channel} Button: ${x.buttonNumber}`}
            items={_existingOutlets}
            class="col-md-6"></Select>
        {#if _selectedOutlet !== undefined}
            <button on:click={async () => await switchPowerOutlet(_selectedOutlet?.switchOnId)} class="btn btn-success"
                >Power On</button>
            <button on:click={async () => await switchPowerOutlet(_selectedOutlet?.switchOffId)} class="btn btn-danger"
                >Power Off</button>
        {/if}
    </div>
    <div class="col-md-6">
        {#if !_previewImage.dataUrl?.isEmpty()}
            <img alt="preview" src={_previewImage.dataUrl} />
        {/if}
        <PictureStreamer bind:this={_pictureStreamer}></PictureStreamer>
        <div class="col-md-12" style="height:80vh;">
            {#if !_webSshLink.isEmpty()}
                <iframe style="height: 100%;width:100%" title="Web SSH" src={_webSshLink}></iframe>
            {/if}
            {#if !_logData.isEmpty()}
                <textarea style="height: 100%; width:100%">{_logData.split("\n").toReversed().join("\n")}</textarea>
            {/if}
        </div>
        <div style="height: 20vh;"></div>
    </div>
</div>
