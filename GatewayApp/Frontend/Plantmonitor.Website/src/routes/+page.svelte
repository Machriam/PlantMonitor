<script lang="ts">
	import Counter from './Counter.svelte';
	let words: string[] = ['asdf', 'bla'];
</script>

<Counter></Counter>
<div>
	{#each words as word}
		<label for="test">{word}<br /></label>
	{/each}
</div>
