using Iot.Device.Camera.Settings;
using Iot.Device.Common;
using System.Diagnostics;
using System.IO.Pipelines;

namespace PlantMonitorControl.Features.MotorMovement;

public class DevelopCameraInterop() : ICameraInterop
{
    public Task<bool> CameraFound()
    {
        throw new NotImplementedException();
    }

    public Task<bool> CameraFunctional()
    {
        throw new NotImplementedException();
    }

    public Task<string> CameraInfo()
    {
        throw new NotImplementedException();
    }

    public Task<IResult> CaptureTestImage()
    {
        throw new NotImplementedException();
    }

    public Task<IResult> TestVideoFile()
    {
        throw new NotImplementedException();
    }

    public (MemoryStream Ms, Task ProcessTask) VideoStream()
    {
        var fs = new FileStream("../TestVideo.mjpeg", FileMode.Open);
        var ms = new MemoryStream();
        var copyCount = 0;
        var task = Task.Run(async () =>
        {
            while (true)
            {
                fs.Position = 0;
                Console.WriteLine("CopyCount: " + copyCount++);
                await fs.CopyToAsync(ms);
                await Task.Delay(1000);
            }
        });
        return (ms, task);
    }

    public (Pipe Pipe, Task ProcessTask) VideoStreamPipe()
    {
        var fs = new FileStream("../TestVideo.mjpeg", FileMode.Open);
        var pipe = new Pipe();
        var copyCount = 0;
        var task = Task.Run(async () =>
        {
            while (true)
            {
                fs.Position = 0;
                Console.WriteLine("CopyCount: " + copyCount++);
                await fs.CopyToAsync(pipe.Writer.AsStream());
                await Task.Delay(1000);
            }
        });
        return (pipe, task);
    }
}