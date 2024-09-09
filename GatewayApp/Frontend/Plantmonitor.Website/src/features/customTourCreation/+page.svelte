<script lang="ts">
    import {CustomTourCreationClient} from "~/services/GatewayAppApi";
    import TextInput from "../reuseableComponents/TextInput.svelte";
    import NumberInput from "../reuseableComponents/NumberInput.svelte";
    import {Task} from "~/types/Task";

    let _uploadFileId = Math.random().toString(36);
    let _uploadStatus = "";
    let _uploadData: {photoTourName: string; comment: string; pixelSizeInMm: number} = {
        photoTourName: "",
        comment: "",
        pixelSizeInMm: 0
    };
    async function uploadFile() {
        const file = document.getElementById(_uploadFileId) as HTMLInputElement;
        if (file.files == null || file.files.length == 0) return;
        const formData = new FormData();
        formData.append("file", file.files[0]);
        const customTourClient = new CustomTourCreationClient();
        _uploadStatus = "Uploading.";
        const promise = customTourClient.uploadFile(
            _uploadData.photoTourName,
            _uploadData.comment,
            _uploadData.pixelSizeInMm.toFixed(4),
            {
                fileName: file.files[0].name,
                data: file.files[0]
            }
        );
        let uploadFinished = false;
        promise
            .then(() => {
                _uploadStatus = "Upload successful.";
                uploadFinished = true;
            })
            .catch((e) => {
                _uploadStatus = "Upload failed: " + e;
                uploadFinished = true;
            });
        while (!uploadFinished) {
            await Task.delay(1000);
            _uploadStatus += ".";
        }
    }
</script>

<svelte:head><title>Custom Tour Creation</title></svelte:head>

<div class="col-md-12 row mt-2">
    <div class="col-md-4" style="align-self: center;">
        <input id={_uploadFileId} type="file" class="form-control" />
    </div>
    <TextInput class="col-md-2" label="Phototour Name" bind:value={_uploadData.photoTourName}></TextInput>
    <TextInput class="col-md-2" label="Phototour Comment" bind:value={_uploadData.comment}></TextInput>
    <NumberInput class="col-md-2" label="Pixel size in mm" bind:value={_uploadData.pixelSizeInMm}></NumberInput>
    <button class="btn btn-primary col-md-2" on:click={uploadFile}>Create new Phototour</button>
    {#if !_uploadStatus.isEmpty()}
        <div>Upload Status: {_uploadStatus}</div>
    {/if}
</div>
