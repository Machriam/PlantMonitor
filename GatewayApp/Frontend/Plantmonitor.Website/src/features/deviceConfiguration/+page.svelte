<script lang="ts">
	'@hmr:keep-all';
	import { onMount } from 'svelte';
	import {
		DeviceConfigurationClient,
		DeviceHealthState,
		WebSshCredentials
	} from '../../services/GatewayAppApi';
	import 'typeExtensions';
	import { Task } from '~/types/task';
	import { ImageTakingClient, MotorMovementClient } from '~/services/PlantMonitorControlApi';

	let configurationClient: DeviceConfigurationClient;
	let devices: DeviceHealthState[] = [];
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
	async function openConsole(ip: string | undefined): Promise<void> {
		if (ip == undefined) return;
		webSshLink = '';
		await Task.delay(100);
		webSshLink = `${webSshCredentials.url}/?hostname=${ip}&username=${webSshCredentials.user}&password=${webSshCredentials.password?.asBase64()}`;
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
		webSshLink = `${webSshCredentials.url}/?hostname=${ip}&username=${webSshCredentials.user}&password=${webSshCredentials.password?.asBase64()}&command=${command.urlEncoded()}`;
	}
	async function showPreviewImage(device: string | undefined) {
		if (device == undefined) return;
		const imageTakingClient = new ImageTakingClient(`https://${device}`).withTimeout(10000);
		previewImage = await (await imageTakingClient.captureImage()).data.asBase64Url();
	}
	async function testMovement(device: string | undefined) {
		if (device == undefined) return;
		const motorMovementClient = new MotorMovementClient(`https://${device}`).withTimeout(10000);
		await motorMovementClient.moveMotor(-1500, 1000, 10000, 300);
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
								<span>{device.health.deviceId}</span>
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
		<div class="col-md-12" style="height:80vh;">
			{#if !webSshLink.isEmpty()}
				<iframe style="height: 100%;width:100%" title="Web SSH" src={webSshLink}></iframe>
			{/if}
		</div>
		<div style="height: 20vh;"></div>
	</div>
</div>
