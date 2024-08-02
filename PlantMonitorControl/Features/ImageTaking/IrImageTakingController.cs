using Microsoft.AspNetCore.Mvc;

namespace PlantMonitorControl.Features.ImageTaking;

[ApiController]
[Route("api/[controller]")]
public class IrImageTakingController([FromKeyedServices(ICameraInterop.IrCamera)] ICameraInterop cameraInterop) : ControllerBase
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

    [HttpPost("runffc")]
    public void RunFFC()
    {
        cameraInterop.CalibrateCamera();
    }

    [HttpGet("countoftakenimages")]
    public int CountOfTakenImages()
    {
        return cameraInterop.CountOfTakenImages();
    }

    [HttpPost("killcamera")]
    public async Task KillCamera()
    {
        await cameraInterop.KillImageTaking();
    }
}
