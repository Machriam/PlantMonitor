<script lang="ts">
	'@hmr:keep-all';
	import { onMount } from 'svelte';
	import { DeviceConfigurationClient, WebSshCredentials } from '../../services/GatewayAppApi';
	import 'typeExtensions';
	import { Task } from '~/types/task';
	import {
		ImageTakingClient,
		WeatherForecast,
		WeatherForecastClient
	} from '~/services/PlantMonitorControlApi';

	let configurationClient: DeviceConfigurationClient;
	let devices: string[] = [];
	let cameraStatus: Map<string, string> = new Map<string, string>();
	let previewImage = '';
	let searchingForDevices = true;
	let webSshLink = ''; // @hmr:keep
	let webSshCredentials: WebSshCredentials;
	onMount(async () => {
		configurationClient = new DeviceConfigurationClient();
		devices = await configurationClient.getDevices();
		webSshCredentials = await configurationClient.getWebSshCredentials();
		searchingForDevices = false;
	});
	async function openConsole(ip: string): Promise<void> {
		webSshLink = '';
		await Task.delay(100);
		webSshLink = `${webSshCredentials.url}/?hostname=${ip}&username=${webSshCredentials.user}&password=${webSshCredentials.password?.asBase64()}`;
	}
	async function configureDevice(ip: string): Promise<void> {
		webSshLink = '';
		await Task.delay(100);
		var certificate = await configurationClient.getCertificateData();
		const command =
			`sudo mkdir /srv/certs/;echo '${certificate.certificate}' | sudo tee /srv/certs/plantmonitor.crt;echo '${certificate.key}' | sudo tee /srv/certs/plantmonitor.key;` +
			` sudo apt-get update;sudo apt-get install -y git;` +
			` git clone https://github.com/Machriam/PlantMonitor.git;cd PlantMonitor; sudo chmod -R 755 *;cd PlantMonitorControl/Install;./install.sh;`;
		webSshLink = `${webSshCredentials.url}/?hostname=${ip}&username=${webSshCredentials.user}&password=${webSshCredentials.password?.asBase64()}&command=${command.urlEncoded()}`;
	}
	async function showPreviewImage(device: string) {
		const imageTakingClient = new ImageTakingClient(`https://${device}`).withTimeout(10000);
		previewImage = 'data:image/png;base64,' + (await imageTakingClient.captureImage());
	}
	async function getDeviceStatus() {
		for (var i = 0; i < devices.length; i++) {
			const imageTakingClient = new ImageTakingClient(`https://${devices[i]}`).withTimeout(10000);
			let result: string | undefined;
			try {
				result = await imageTakingClient.getCameras();
				cameraStatus.set(devices[i], result);
			} catch (ex) {
				cameraStatus.set(devices[i], 'NA');
			}
		}
		cameraStatus = cameraStatus;
	}
</script>

<div class="col-md-12 row">
	<h3>
		{#if searchingForDevices}
			Searching for devices
		{:else}
			Found devices:
		{/if}
	</h3>
	<div class="col-md-12">
		{#each devices as device}
			<table class="table">
				<thead>
					<tr>
						<th>IP</th>
						<th>Action</th>
						<th>Data</th>
					</tr>
				</thead>
				<tbody>
					<tr>
						<td>{device}</td>
						<td class="d-flex flex-row justify-content-between">
							<button on:click={() => configureDevice(device)} class="btn btn-primary">
								Configure
							</button>
							<button on:click={() => showPreviewImage(device)} class="btn btn-primary">
								Preview Image
							</button>
							<button on:click={() => openConsole(device)} class="btn btn-primary">
								Open Console
							</button>
						</td>
						<td>{cameraStatus.get(device)}</td>
					</tr>
				</tbody>
			</table>
		{/each}
		<button on:click={async () => await getDeviceStatus()} class="btn btn-primary">Update</button>
	</div>
	<div class="col-md-12"></div>
	<div class="col-md-4">
		{#if !previewImage.isEmpty()}
			<img alt="Preview" src={previewImage} />
		{/if}
	</div>
	<div class="col-md-8" style="height:80vh;">
		{#if !webSshLink.isEmpty()}
			<iframe style="height: 100%;width:100%" title="Web SSH" src={webSshLink}></iframe>
		{/if}
	</div>
</div>
