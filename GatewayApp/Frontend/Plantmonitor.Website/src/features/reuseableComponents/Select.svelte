<script lang="ts" generics="T">
    export let idSelector: (x: T) => string = (x) => JSON.stringify(x);
    export let textSelector: (x: T) => string = (x) => JSON.stringify(x);
    export let items: T[];
    export let initialSelectedItem: string | undefined = "";
    let selectedItem: T | undefined;
    export let selectedItemChanged: (x: T | undefined) => void;
    function onChange(event: Event & {currentTarget: EventTarget & HTMLSelectElement}) {
        const id = event.currentTarget.value;
        selectedItem = items.find((x) => idSelector(x) == id);
        selectedItemChanged(selectedItem);
    }
    $: {
        selectedItem = items.find((x) => idSelector(x) == initialSelectedItem);
    }
    function isSelected(item: T) {
        return selectedItem == item;
    }
</script>

<div class={$$restProps.class || ""}>
    <select class="form-select" on:change={(x) => onChange(x)}>
        <option value="-1">NA</option>
        {#each items as item}
            <option value={idSelector(item)} selected={isSelected(item)}>{textSelector(item)}</option>
        {/each}
    </select>
</div>
