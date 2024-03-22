using Iot.Device.Camera;
using Iot.Device.Camera.Settings;
using Iot.Device.Common;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices.Marshalling;

namespace PlantMonitorControl.Features.MotorMovement;

[ApiController]
[Route("[controller]")]
public class ImageTakingController : ControllerBase
{
    [HttpPost("previewimage")]
    public async Task<byte[]> CaptureImage()
    {
        var builder = new CommandOptionsBuilder()
            .WithTimeout(1)
            .WithVflip()
            .WithHflip()
            .WithPictureOptions(100, "png")
            .WithResolution(640, 480);
        var args = builder.GetArguments();
        using var process = new ProcessRunner(ProcessSettingsFactory.CreateForLibcamerastill());

        var filename = "test.png";
        System.IO.File.Delete(filename);
        using var file = System.IO.File.OpenWrite("test.png");
        await process.ExecuteAsync(args, file);
        return System.IO.File.ReadAllBytes(filename);
    }

    [HttpGet("camerainfo")]
    public async Task<string> GetCameras()
    {
        var builder = new CommandOptionsBuilder().WithListCameras();
        var args = builder.GetArguments();

        using var process = new ProcessRunner(ProcessSettingsFactory.CreateForLibcamerastill());
        return await process.ExecuteReadOutputAsStringAsync(args);
    }
}