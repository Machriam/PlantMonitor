<script lang="ts">
    import {goto} from "$app/navigation";
    import {onMount} from "svelte";
    import {navigationChanged} from "./store";

    onMount(() => {
        $navigationChanged = "";
        $navigationChanged = "dashboard";
        goto("/dashboard");
    });
</script>
