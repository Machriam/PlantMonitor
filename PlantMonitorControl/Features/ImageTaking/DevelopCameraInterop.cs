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
        using var fs = new FileStream("../TestVideo.mjpeg", FileMode.Open);
        var ms = new MemoryStream();
        fs.CopyTo(ms);
        ms.Position = 0;
        return (ms, Task.CompletedTask);
    }
}