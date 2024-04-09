using Iot.Device.Camera.Settings;
using Iot.Device.Common;
using Iot.Device.Media;
using Microsoft.AspNetCore.Mvc;
using System.IO.Pipelines;

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

    [HttpGet("videostream")]
    public async Task<IActionResult> GetVideoStream()
    {
        var (stream, process) = cameraInterop.VideoStream();
        return new FileStreamResult(stream, "video/mjpeg");
        //var pipeReader = PipeReader.Create(stream);
        //byte[] buffer = new byte[1024 * 4];
        //long startPosition = 0;
        //if (!string.IsNullOrEmpty(Request.Headers.Range))
        //{
        //    string[] range = Request.Headers.Range.ToString().Split(['=', '-']);
        //    startPosition = long.Parse(range[1]);
        //}

        //Response.StatusCode = StatusCodes.Status206PartialContent;
        //Response.Headers.AcceptRanges = "bytes";
        //Response.Headers.ContentRange = string.Format($" bytes {}");
        //pipeReader.AdvanceTo().TryRead()
    }

    [HttpGet("videotest")]
    public async Task<IResult> GetVideoTest()
    {
        return await cameraInterop.TestVideoFile();
    }
}