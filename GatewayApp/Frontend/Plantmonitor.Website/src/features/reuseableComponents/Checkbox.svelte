<script lang="ts">
    export let disabledSelector = () => false;
    export let value = false;
    export let label = "";
    const guid = crypto.randomUUID();
</script>

<div class="{$$restProps.class || ''} row ms-1 form-switch">
    <input disabled={disabledSelector()} class="form-check-input col-md-2" type="checkbox" bind:checked={value} id={guid} />
    <label class="col-md-9 form-check-label" for={guid}>{label}</label>
</div>
