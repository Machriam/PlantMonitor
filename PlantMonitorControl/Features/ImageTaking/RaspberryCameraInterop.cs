using Iot.Device.Camera.Settings;
using Iot.Device.Common;
using System.Diagnostics;

namespace PlantMonitorControl.Features.MotorMovement;

public interface ICameraInterop
{
    public const string VisCamera = nameof(VisCamera);
    public const string IrCamera = nameof(IrCamera);

    Task<bool> CameraFound();

    Task<bool> CameraFunctional();

    Task<string> CameraInfo();

    Task<IResult> CaptureTestImage();

    Task<IResult> VideoStream();
}

public class RaspberryCameraInterop() : ICameraInterop
{
    private bool _cameraFound;
    private bool _deviceFunctional;

    private readonly ProcessSettings _videoProcessSettings = new() { Filename = "rpicam-vid", WorkingDirectory = null };
    private readonly ProcessSettings _imageProcessSettings = new() { Filename = "rpicam-still", WorkingDirectory = null };

    public async Task<bool> CameraFunctional()
    {
        if (!_deviceFunctional) await CaptureTestImage();
        return _deviceFunctional;
    }

    public async Task<bool> CameraFound()
    {
        if (_cameraFound) return _cameraFound;
        var info = await CameraInfo();
        if (!info.Contains("no camera", StringComparison.InvariantCultureIgnoreCase) && info.Length > 10) _cameraFound = true;
        return _cameraFound;
    }

    public async Task<IResult> VideoStream()
    {
        var builder = new CommandOptionsBuilder()
        .WithContinuousStreaming()
        .WithVflip()
        .WithHflip()
        .WithMJPEGVideoOptions(100)
        .WithResolution(640, 480);
        var args = builder.GetArguments();
        using var process = new ProcessRunner(_videoProcessSettings);

        var ms = new MemoryStream();
        var task = await process.ContinuousRunAsync(args, ms);
        await Task.Delay(2000);
        process.Dispose();
        try { await task; Console.WriteLine("No Error"); } catch (Exception ex) { Console.WriteLine(ex.Message); }
        var info = new ProcessStartInfo("pkill", $"-9 -f {_videoProcessSettings.Filename}");
        new Process() { StartInfo = info }.Start();
        ms.Position = 0;
        var success = ms.Length > 1000;
        _deviceFunctional = success;
        _cameraFound |= success;
        return Results.File(ms, "video/mp4");
    }

    public async Task<string> CameraInfo()
    {
        var builder = new CommandOptionsBuilder().WithListCameras();
        var args = builder.GetArguments();

        using var process = new ProcessRunner(_imageProcessSettings);
        return await process.ExecuteReadOutputAsStringAsync(args);
    }

    public async Task<IResult> CaptureTestImage()
    {
        var builder = new CommandOptionsBuilder()
                .WithTimeout(1)
                .WithVflip()
                .WithHflip()
                .WithPictureOptions(100, "png")
                .WithResolution(640, 480);
        var args = builder.GetArguments();
        using var process = new ProcessRunner(_imageProcessSettings);

        var ms = new MemoryStream();
        await process.ExecuteAsync(args, ms);
        ms.Position = 0;
        var success = ms.Length > 1000;
        _deviceFunctional = success;
        _cameraFound |= success;
        return Results.File(ms, "image/png");
    }
}