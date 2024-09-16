<script lang="ts">
    import "typeExtensions";
    import Header from "./app/Navbar.svelte";
    import DeviceSelection from "./reuseableComponents/DeviceSelection.svelte";
    import {allDevices, selectedDevice} from "./store";
    import "./styles.css";
</script>

<div class="page">
    <div class="sidebar">
        <Header />
    </div>

    <main>
        <div class="top-row px-4">
            <DeviceSelection
                refreshTimeInSeconds={2}
                on:allDevices={(x) => ($allDevices = x.detail)}
                on:select={(x) => ($selectedDevice = x.detail)}
                class="col-md-12 d-flex flex-row"></DeviceSelection>
        </div>
        <article style="width:87vw" class="content px-4">
            <slot />
        </article>
    </main>

    <footer></footer>
</div>

<style>
    .page {
        position: relative;
        display: flex;
        flex-direction: column;
    }

    main {
        flex: 1;
    }

    .sidebar {
        background-image: linear-gradient(180deg, rgb(5, 39, 103) 0%, #3a0647 70%);
    }

    .top-row {
        background-color: #f7f7f7;
        border-bottom: 1px solid #d6d5d5;
        justify-content: flex-end;
        height: 3.5rem;
        display: flex;
        align-items: center;
    }

    @media (max-width: 640.98px) {
        .top-row {
            justify-content: space-between;
        }
    }

    @media (min-width: 641px) {
        .page {
            flex-direction: row;
        }

        .sidebar {
            width: 250px;
            height: 100vh;
            position: sticky;
            top: 0;
        }

        .top-row {
            position: sticky;
            top: 0;
            z-index: 1;
        }

        .top-row,
        article {
            padding-left: 2rem !important;
            padding-right: 1.5rem !important;
        }
    }
</style>
