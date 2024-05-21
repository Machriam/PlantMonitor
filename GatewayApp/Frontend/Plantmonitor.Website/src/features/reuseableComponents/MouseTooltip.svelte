<svelte:options customElement="mouse-tooltip" />

<script lang="ts">
    export let value: string = "";
    export let label = "";
    let id = Math.random() * 1_000_000_000 + "";
    export let top = 20;
    export let left = 20;
</script>

<div style="position:absolute;background-color:white;left: {left}px;top: {top}px" {id}>
    <span>{label}</span>
    <span>{value}</span>
</div>
