<script lang="ts">
	"@hmr:keep-all";
	import {onMount} from "svelte";
	import {
		AppConfigurationClient,
		DeviceClient,
		DeviceConfigurationClient,
		DeviceHealthState,
		HealthState,
		WebSshCredentials
	} from "../../services/GatewayAppApi";
	import "typeExtensions";
	import {Task} from "~/types/task";
	import {CvInterop} from "./CvInterop";
	import TextInput from "../reuseableComponents/TextInput.svelte";
	import PasswordInput from "../reuseableComponents/PasswordInput.svelte";
	import {DeviceStreaming as DeviceStreamingApi} from "~/services/DeviceStreaming";

	const videoCanvasId = "videoCanvasId";
	let configurationData: {ipFrom: string; ipTo: string; userName: string; userPassword: string} =
		{
			ipFrom: "",
			ipTo: "",
			userName: "",
			userPassword: ""
		};
	let configurationClient: DeviceConfigurationClient;
	let devices: DeviceHealthState[] = [];
	let previewImage = "";
	let frameCounter = 0;

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
		devices = await configurationClient.getDevices();
		webSshCredentials = await configurationClient.getWebSshCredentials();
		searchingForDevices = false;
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
	async function showPreviewImage(device: string | undefined) {
		if (device == undefined) return;
		const deviceClient = new DeviceClient();
		await deviceClient.killCamera(device);
		previewImage = await (await deviceClient.previewImage(device)).data.asBase64Url();
	}
	async function testMovement(device: string | undefined) {
		if (device == undefined) return;
		const deviceClient = new DeviceClient();
		await deviceClient.move(device, -1500, 1000, 10000, 300);
	}
	async function showTestVideo(device: string | undefined) {
		if (device == undefined) return;
		const connection = new DeviceStreamingApi().buildVideoConnection(device);
		const cvInterop = new CvInterop();
		const image = document.getElementById(videoCanvasId) as HTMLImageElement;
		const videoDisplayFunction = cvInterop.displayVideoBuilder(image);
		connection.start(async (image) => {
			frameCounter++;
			await videoDisplayFunction(image);
		});
	}
	async function updateIpRange() {
		const client = new AppConfigurationClient();
		client.updateIpRanges(configurationData.ipFrom, configurationData.ipTo);
		configurationData.ipFrom = "";
		configurationData.ipTo = "";
	}
	async function updateDeviceSettings() {
		const client = new AppConfigurationClient();
		await client.updateDeviceSettings(
			configurationData.userPassword,
			configurationData.userName
		);
		configurationClient = new DeviceConfigurationClient();
		webSshCredentials = await configurationClient.getWebSshCredentials();
		configurationData.userName = "";
		configurationData.userPassword = "";
	}
	async function getDeviceStatus() {
		const client = new DeviceConfigurationClient();
		try {
			devices = await client.getDevices();
		} catch (ex) {
			devices = [];
		}
	}
</script>

<div class="col-md-12 row">
	<h3>Plantmonitor Configuration</h3>
	<div class="col-md-12 row">
		<TextInput class="col-md-2" bind:value={configurationData.ipFrom} label="IP From"
		></TextInput>
		<TextInput class="col-md-2" bind:value={configurationData.ipTo} label="IP To"></TextInput>
		<button on:click={updateIpRange} class="btn btn-primary col-md-2">Update IP</button>
		<div class="col-md-12 mt-2"></div>
		<TextInput class="col-md-2" bind:value={configurationData.userName} label="Device SSH User"
		></TextInput>
		<PasswordInput
			class="col-md-2"
			bind:value={configurationData.userPassword}
			label="Device SSH Password"></PasswordInput>
		<button on:click={updateDeviceSettings} class="btn btn-primary col-md-2"
			>Update Password</button>
	</div>
	<h3>
		{#if searchingForDevices}
			Searching for devices
		{:else}
			Found devices:
		{/if}
	</h3>
	<div>{frameCounter}</div>
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
							<button
								on:click={() => configureDevice(device.ip)}
								class="btn btn-primary">
								Configure
							</button>
							{#if device.health != undefined}
								<button
									on:click={() => showPreviewImage(device.ip)}
									class="btn btn-primary">
									Preview Image
								</button>
								<button
									on:click={() => showTestVideo(device.ip)}
									class="btn btn-primary">
									Preview Video
								</button>
								<button
									on:click={() => testMovement(device.ip)}
									class="btn btn-primary">
									Test Movement
								</button>
							{/if}
							<button on:click={() => openConsole(device.ip)} class="btn btn-primary">
								Open Console
							</button>
						</td>
					</tr>
				</tbody>
			</table>
		{/each}
		<button on:click={async () => await getDeviceStatus()} class="btn btn-primary"
			>Update</button>
	</div>
	<div class="col-md-6">
		<div class="col-md-12">
			{#if !previewImage.isEmpty()}
				<img alt="Preview" src={previewImage} />
			{/if}
		</div>
		<div class="col-md-12"><img alt="Video" id={videoCanvasId} /></div>
		<div class="col-md-12" style="height:80vh;">
			{#if !webSshLink.isEmpty()}
				<iframe style="height: 100%;width:100%" title="Web SSH" src={webSshLink}></iframe>
			{/if}
		</div>
		<div style="height: 20vh;"></div>
	</div>
</div>
