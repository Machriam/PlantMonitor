<script lang="ts">
    import {onDestroy, onMount} from "svelte";
    import {
        AutomaticPhotoTourClient,
        TemperatureClient,
    } from "~/services/GatewayAppApi";

    onDestroy(() => {
    });
    onMount(async () => {
        const photoTourClient = new AutomaticPhotoTourClient();
        const temperatureClient = new TemperatureClient();
    });

</script>

<div class="col-md-12 row">
    asdf
</div>
