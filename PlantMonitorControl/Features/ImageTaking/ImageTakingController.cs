using Iot.Device.Camera;
using Iot.Device.Camera.Settings;
using Iot.Device.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.Marshalling;

namespace PlantMonitorControl.Features.MotorMovement;

[ApiController]
[Route("[controller]")]
public class ImageTakingController : ControllerBase
{
    [HttpPost("previewimage")]
    public async Task<IActionResult> CaptureImage()
    {
        var builder = new CommandOptionsBuilder()
            .WithTimeout(1)
            .WithVflip()
            .WithHflip()
            .WithPictureOptions(100, "png")
            .WithResolution(640, 480);
        var args = builder.GetArguments();
        using var process = new ProcessRunner(ProcessSettingsFactory.CreateForLibcamerastill());

        await using var ms = new MemoryStream();
        await process.ExecuteAsync(args, Response.Body);
        return Ok(ms);
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