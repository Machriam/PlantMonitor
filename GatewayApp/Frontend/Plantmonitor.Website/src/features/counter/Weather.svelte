<script lang="ts">
	import { onMount } from 'svelte';
	import { WeatherForecast, WeatherForecastClient } from '../../services/GeneratedApi';
	let _weatherData: WeatherForecast[] = [];

	onMount(async () => {
		let forecastService = new WeatherForecastClient('https://localhost:7005');
		_weatherData = await forecastService.get();
	});
	function log() {}
</script>

<h1>Weather Forecast</h1>
<button on:click={log} class="btn btn-primary">bla2</button>
<div>
	<table class="table">
		<thead>
			<tr>
				<th>1</th>
				<th>2</th>
				<th>3</th>
				<th>4</th>
			</tr>
		</thead>
		<tbody>
			{#each _weatherData as forecast}
				<tr class="table-row">
					<td class="text-slate-400">{forecast.date?.toDateString()}</td>
					<td>{forecast.summary}</td>
					<td>{forecast.temperatureC}</td>
					<td>{forecast.temperatureF}</td>
				</tr>
			{/each}
		</tbody>
	</table>
</div>
