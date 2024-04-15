<script lang="ts">
	'@hmr:keep-all';
	import { onMount } from 'svelte';
	import {
		DeviceConfigurationClient,
		DeviceHealthState,
		HealthState,
		WebSshCredentials
	} from '../../services/GatewayAppApi';
	import 'typeExtensions';
	import * as signalR from '@microsoft/signalr';
	import * as signalRProtocols from '@microsoft/signalr-protocol-msgpack';
	import { Task } from '~/types/task';
	import { ImageTakingClient, MotorMovementClient } from '~/services/PlantMonitorControlApi';
	import { CvInterop } from './CvInterop';
	import { dev } from '$app/environment';

	const videoCanvasId = 'videoCanvasId';
	let configurationClient: DeviceConfigurationClient;
	let devices: DeviceHealthState[] = [];
	let previewImage = '';
	let previewVideo = '';
	let frameCounter = 0;
	let connection: signalR.HubConnection;
	let searchingForDevices = true;
	let webSshLink = ''; // @hmr:keep
	function healthStateFormatter(state: HealthState) {
		const result = Object.getOwnPropertyNames(HealthState)
			.filter((x) => !parseInt(x) && x != '0')
			.filter((x) => (HealthState as unknown as { [key: string]: number })[x] & state);
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
		webSshLink = '';
		await Task.delay(100);
		webSshLink = `${webSshCredentials.protocol}://${location.hostname}:${webSshCredentials.port}/?hostname=${ip}&username=${webSshCredentials.user}&password=${webSshCredentials.password?.asBase64()}`;
	}
	async function configureDevice(ip: string | undefined): Promise<void> {
		if (ip == undefined) return;
		webSshLink = '';
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
		const imageTakingClient = new ImageTakingClient(`https://${device}`).withTimeout(10000);
		await imageTakingClient.killCamera();
		previewImage = await (await imageTakingClient.previewImage()).data.asBase64Url();
	}
	async function testMovement(device: string | undefined) {
		if (device == undefined) return;
		const motorMovementClient = new MotorMovementClient(`https://${device}`).withTimeout(10000);
		await motorMovementClient.moveMotor(-1500, 1000, 10000, 300);
	}
	async function showTestVideo(device: string | undefined) {
		if (device == undefined) return;
		await connection?.stop();
		connection = new signalR.HubConnectionBuilder()
			.withUrl(`https://${device}/hub/video`, { withCredentials: false })
			.withHubProtocol(new signalRProtocols.MessagePackHubProtocol())
			.build();
		await connection.start();
		const cvInterop = new CvInterop();
		const image = document.getElementById(videoCanvasId) as HTMLImageElement;
		const videoDisplayFunction = cvInterop.displayVideoBuilder(image);
		connection.stream('StreamMjpeg', 2, 100, 0.1).subscribe({
			next: async (x) => {
				frameCounter++;
				const payload = x as Uint8Array;
				const blob = new Blob([payload], { type: 'image/jpeg' });
				await videoDisplayFunction(await blob.asBase64Url());
			},
			complete: () => console.log('complete'),
			error: (x) => console.log(x)
		});
	}
	async function getDeviceStatus() {
		let client: DeviceConfigurationClient;
		if (dev) client = new DeviceConfigurationClient();
		else client = new DeviceConfigurationClient(`https://${location.hostname}`);
		try {
			devices = await client.getDevices();
		} catch (ex) {
			devices = [];
		}
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
	<div>{frameCounter}</div>
	<button class="btn btn-primary" on:click={() => showTestVideo('localhost:7127')}
		>Local Streaming Test</button
	>
	<div class="col-md-6">
		{#each devices as device}
			<table class="table">
				<thead>
					<tr>
						<th>IP</th>
						<th>Action</th>
					</tr>
				</thead>
				<tbody>
					<tr>
						<td class="col-md-3">
							{#if device.health != undefined}
								<span class="badge bg-success">{device.ip}</span><br />
								<span>{device.health.deviceName}</span><br />
								<span>{device.health.deviceId}</span><br />
								<span>{healthStateFormatter(device.health.state ?? HealthState.NA)}</span>
							{:else}
								<span class="badge bg-danger">{device.ip}</span>
							{/if}
						</td>
						<td>
							<button on:click={() => configureDevice(device.ip)} class="btn btn-primary">
								Configure
							</button>
							{#if device.health != undefined}
								<button on:click={() => showPreviewImage(device.ip)} class="btn btn-primary">
									Preview Image
								</button>
								<button on:click={() => showTestVideo(device.ip)} class="btn btn-primary">
									Preview Video
								</button>
								<button on:click={() => testMovement(device.ip)} class="btn btn-primary">
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
		<button on:click={async () => await getDeviceStatus()} class="btn btn-primary">Update</button>
	</div>
	<div class="col-md-6">
		<div class="col-md-12">
			{#if !previewImage.isEmpty()}
				<img alt="Preview" src={previewImage} />
			{/if}
		</div>
		<div class="col-md-12">
			<img alt="Video" id={videoCanvasId} />
		</div>
		<div class="col-md-12" style="height:80vh;">
			{#if !webSshLink.isEmpty()}
				<iframe style="height: 100%;width:100%" title="Web SSH" src={webSshLink}></iframe>
			{/if}
		</div>
		<div style="height: 20vh;"></div>
	</div>
</div>
