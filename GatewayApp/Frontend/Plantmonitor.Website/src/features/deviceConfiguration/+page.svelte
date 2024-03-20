<script lang="ts">
	import { onMount } from 'svelte';
	import { DeviceConfigurationClient } from '../../services/GeneratedApi';
	import 'typeExtensions';
	import { Task } from '~/types/task';

	let devices: string[] = [];
	let searchingForDevices = true;
	let webSshLink = '';
	onMount(async () => {
		let configurationClient = new DeviceConfigurationClient();
		devices = await configurationClient.getDevices();
		searchingForDevices = false;
	});
	async function openConsole(ip: string): Promise<void> {
		webSshLink = '';
		await Task.delay(100);
		webSshLink = `http://localhost:8888/?hostname=${ip}&username=plantmonitor&password=${'plantmonitor'.asBase64()}`;
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
							<button class="btn btn-primary">Configure</button>
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
