<script lang="ts">
	import Counter from './Counter.svelte';
	import Weather from './Weather.svelte';
	let words: string[] = ['asdf', 'bla'];
</script>

<Counter></Counter>
<Weather></Weather>
<div>
	{#each words as word}
		<label for="test">{word}<br /></label>
	{/each}
</div>
