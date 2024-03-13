<script lang="ts">
	import { onMount } from 'svelte';
	import { WeatherForecast, WeatherForecastClient } from '../../services/GeneratedApi';
	let _weatherData: WeatherForecast[] = [];

	onMount(async () => {
		console.log('mounted');
		let forecastService = new WeatherForecastClient('https://localhost:7005');
		_weatherData = await forecastService.get();
	});
</script>

<h1>Weather Forecast</h1>
<div>
	<table class="table-auto">
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
				<tr>
					<td>{forecast.date?.toDateString()}</td>
					<td>{forecast.summary}</td>
					<td>{forecast.temperatureC}</td>
					<td>{forecast.temperatureF}</td>
				</tr>
				{/each}
		</tbody>
	</table>
</div>
