<script lang="ts">
    export let disabledSelector = () => false;
    export let value = 0;
    export let label = "";
    export let step = 1;
    const guid = crypto.randomUUID();
</script>

<div class="{$$restProps.class || ''} form-floating">
    <input {step} disabled={disabledSelector()} class="form-control" type="number" placeholder={label} bind:value id={guid} />
    <label class="ms-1" for={guid}>{label}</label>
</div>
