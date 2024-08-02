using Microsoft.AspNetCore.Mvc;

namespace PlantMonitorControl.Features.ImageTaking;

[ApiController]
[Route("api/[controller]")]
public class VisImageTakingController([FromKeyedServices(ICameraInterop.VisCamera)] ICameraInterop cameraInterop) : ControllerBase
{
    [HttpPost("previewimage")]
    public async Task<IResult> PreviewImage()
    {
        return await cameraInterop.CaptureTestImage();
    }

    [HttpGet("camerainfo")]
    public async Task<string> GetCameras()
    {
        return await cameraInterop.CameraInfo();
    }

    [HttpGet("countoftakenimages")]
    public int CountOfTakenImages()
    {
        return cameraInterop.CountOfTakenImages();
    }

    [HttpPost("detectandstoreexposure")]
    public void DetectAndStoreExposure()
    {
        cameraInterop.CalibrateCamera();
    }

    [HttpPost("killcamera")]
    public async Task KillCamera()
    {
        await cameraInterop.KillImageTaking();
    }
}
