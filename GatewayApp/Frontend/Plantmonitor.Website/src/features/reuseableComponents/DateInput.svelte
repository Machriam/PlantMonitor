<script lang="ts">
    export let disabledSelector = () => false;
    export let dateValue = new Date();
    export let value = new Date().toISOString();
    export let label = "";
    const guid = crypto.randomUUID();
    export let valueHasChanged: (newValue: Date) => void = () => {};
    $: if (valueHasChanged) {
        if (typeof value === "string") {
            dateValue = new Date(value);
        }
        valueHasChanged(dateValue);
    }
</script>

<div class="{$$restProps.class || ''} form-floating">
    <input disabled={disabledSelector()} class="form-control" type="datetime-local" placeholder={label} bind:value id={guid} />
    <label class="ms-1" for={guid}>{label}</label>
</div>
