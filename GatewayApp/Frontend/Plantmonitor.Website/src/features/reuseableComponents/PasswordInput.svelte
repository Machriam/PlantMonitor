<script lang="ts">
	export let disabledSelector = () => false;
	export let value = "";
	export let label = "";
	const guid = crypto.randomUUID();
</script>

<div class="{$$restProps.class || ''} form-floating">
	<input
		disabled={disabledSelector()}
		class="form-control"
		type="password"
		placeholder={label}
		bind:value
		id={guid} />
	<label class="ms-1" for={guid}>{label}</label>
</div>
