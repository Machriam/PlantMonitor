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

    public bool CameraIsRunning()
    {
        throw new NotImplementedException();
    }

    public Task<IResult> CaptureTestImage()
    {
        throw new NotImplementedException();
    }

    public Task KillImageTaking()
    {
        throw new NotImplementedException();
    }

    public Task<(Pipe Pipe, Task ProcessTask)> MjpegStream(float resolutionDivider, int quality, float distanceInM)
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
                await Task.Delay(10);
            }
        });
        return Task.FromResult((pipe, task));
    }

    public Task<string> StreamJpgToFolder(float resolutionDivider, int quality, float distanceInM)
    {
        throw new NotImplementedException();
    }
}
