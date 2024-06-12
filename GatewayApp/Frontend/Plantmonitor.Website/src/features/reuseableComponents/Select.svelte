<script lang="ts" generics="T">
    export let idSelector: (x: T) => string = (x) => JSON.stringify(x);
    export let textSelector: (x: T) => string = (x) => JSON.stringify(x);
    export let items: T[];
    export let initialSelectedItem: string | undefined = undefined;
    let firstRender = true;
    let selectedItem: T | undefined;
    let isInitialized = false;
    export let selectedItemChanged: (x: T | undefined) => void;
    function onChange(event: Event & {currentTarget: EventTarget & HTMLSelectElement}) {
        const id = event.currentTarget.value;
        selectedItem = items.find((x) => idSelector(x) == id);
        selectedItemChanged(selectedItem);
    }
    $: {
        if (firstRender) {
            firstRender = false;
        } else {
            selectedItem = items.find((x) => idSelector(x) == initialSelectedItem);
            isInitialized = true;
        }
    }
    function isSelected(item: T) {
        return selectedItem == item;
    }
</script>

{#if isInitialized}
    <div class={$$restProps.class || ""}>
        <select class="form-select" on:change={(x) => onChange(x)}>
            <option value="-1">NA</option>
            {#each items as item}
                <option value={idSelector(item)} selected={isSelected(item)}>{textSelector(item)}</option>
            {/each}
        </select>
    </div>
{/if}
