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
    public async Task GetVideoStream()
    {
        var (stream, process) = cameraInterop.VideoStream();
        var headerBytes = new byte[] { 255, 216, 255, 224, 0, 16, 74, 70, 73, 70, 0 };
        byte[] buffer = new byte[1024];
        var headerIndex = 0;
        bool FindHeader(byte currentByte)
        {
            if (currentByte == headerBytes[headerIndex]) headerIndex++;
            else headerIndex = 0;
            var headerFound = headerIndex == headerBytes.Length;
            if (headerFound) headerIndex = 0;
            return headerFound;
        }
        var startPosition = 0L;
        if (!string.IsNullOrEmpty(Request.Headers.Range))
        {
            string[] range = Request.Headers.Range.ToString().Split(new char[] { '=', '-' });
            startPosition = long.Parse(range[1]);
        }
        Response.StatusCode = StatusCodes.Status206PartialContent;
        Response.Headers.AcceptRanges = "bytes";
        Response.Headers.ContentRange = string.Format($" bytes {startPosition}-{buffer.Length - 1}/*");
        Response.ContentType = "application/octet-stream";
        while (true)
        {
            await Task.Yield();
            int bytesRead = await stream.ReadAsync(buffer);
            if (bytesRead == 0) break;
            for (var i = 0; i < bytesRead; i++)
            {
                var currentByte = buffer[i];
                var headerFound = FindHeader(currentByte);
                if (headerFound) Console.WriteLine("Header Found");
            }
            await Response.BodyWriter.AsStream(true).WriteAsync(buffer);
        };
    }

    [HttpGet("videotest")]
    public async Task<IResult> GetVideoTest()
    {
        return await cameraInterop.TestVideoFile();
    }
}