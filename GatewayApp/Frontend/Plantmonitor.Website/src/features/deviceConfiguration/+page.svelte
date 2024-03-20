<script lang="ts">
	import { onMount } from 'svelte';
	import { DeviceConfigurationClient, WebSshCredentials } from '../../services/GatewayAppApi';
	import 'typeExtensions';
	import { Task } from '~/types/task';
	import { WeatherForecast, WeatherForecastClient } from '~/services/PlantMonitorControlApi';

	let configurationClient: DeviceConfigurationClient;
	let devices: string[] = [];
	let weatherData: Map<string, WeatherForecast[]> = new Map<string, WeatherForecast[]>();
	let searchingForDevices = true;
	let webSshLink = '';
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
	async function getDeviceStatus() {
		for (var i = 0; i < devices.length; i++) {
			const weatherForecastClient = new WeatherForecastClient(`https://${devices[i]}`);
			let result: WeatherForecast[] | undefined;
			try {
				result = await weatherForecastClient.get();
				weatherData.set(devices[i], result);
			} catch (ex) {
				weatherData.set(devices[i], []);
			}
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
	<div class="col-md-4">
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
							<button on:click={() => openConsole(device)} class="btn btn-primary">
								Open Console
							</button>
						</td>
						<td>{weatherData.get(device)?.asJson()}</td>
					</tr>
				</tbody>
			</table>
		{/each}
		<button on:click={async () => await getDeviceStatus()} class="btn btn-primary">Update</button>
	</div>
	{#if !webSshLink.isEmpty()}
		<div class="col-md-8" style="height:80vh;">
			<iframe style="height: 100%;width:100%" title="Web SSH" src={webSshLink}></iframe>
		</div>
	{/if}
</div>
