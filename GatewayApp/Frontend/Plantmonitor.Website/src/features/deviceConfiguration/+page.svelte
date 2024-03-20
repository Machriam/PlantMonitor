<script lang="ts">
	import { onMount } from 'svelte';
	import { DeviceConfigurationClient, WebSshCredentials } from '../../services/GatewayAppApi';
	import 'typeExtensions';
	import { Task } from '~/types/task';

	let devices: string[] = [];
	let searchingForDevices = true;
	let webSshLink = '';
	let webSshCredentials: WebSshCredentials;
	onMount(async () => {
		let configurationClient = new DeviceConfigurationClient();
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
		const command = 'sudo apt-get update;sudo apt-get install -y git;git clone https://github.com/Machriam/PlantMonitor.git;cd PlantMonitor;sudo chmod -R 755 *;cd RaspberryApp/Install;./install.sh;';
		webSshLink = `${webSshCredentials.url}/?hostname=${ip}&username=${webSshCredentials.user}&password=${webSshCredentials.password?.asBase64()}&command=${command.urlEncoded()}`;
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
					</tr>
				</tbody>
			</table>
		{/each}
	</div>
	{#if !webSshLink.isEmpty()}
		<div class="col-md-8" style="height:100vh;">
			<iframe style="height: 100%;width:100%" title="Web SSH" src={webSshLink}></iframe>
		</div>
	{/if}
</div>
