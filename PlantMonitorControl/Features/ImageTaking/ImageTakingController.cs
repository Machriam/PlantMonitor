using Iot.Device.Camera.Settings;
using Iot.Device.Common;
using Iot.Device.Media;
using Microsoft.AspNetCore.Mvc;

namespace PlantMonitorControl.Features.MotorMovement;

[ApiController]
[Route("api/[controller]")]
public class ImageTakingController([FromKeyedServices(ICameraInterop.VisCamera)] ICameraInterop cameraInterop) : ControllerBase
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

    [HttpGet("videotest")]
    public async Task<IResult> GetVideoTest()
    {
        return await cameraInterop.VideoStream();
    }
}