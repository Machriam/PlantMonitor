using Iot.Device.Camera.Settings;
using Iot.Device.Common;
using System.Diagnostics;
using System.IO.Pipelines;

namespace PlantMonitorControl.Features.MotorMovement;

public interface ICameraInterop
{
    public const string VisCamera = nameof(VisCamera);
    public const string IrCamera = nameof(IrCamera);

    Task<bool> CameraFound();

    Task<bool> CameraFunctional();

    Task<string> CameraInfo();

    Task<IResult> CaptureTestImage();

    Task<(Pipe Pipe, Task ProcessTask)> MjpegStream(float resolutionDivider, int quality);
}

public class RaspberryCameraInterop(ILogger<RaspberryCameraInterop> logger) : ICameraInterop
{
    private const int maxWidth = 2304;
    private const int maxHeight = 1296;
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

    public async Task<(Pipe Pipe, Task ProcessTask)> MjpegStream(float resolutionDivider, int quality)
    {
        var width = (int)(maxWidth / resolutionDivider);
        var height = (int)(maxHeight / resolutionDivider);
        var info = new ProcessStartInfo("pkill", $"-9 -f {_videoProcessSettings.Filename}");
        new Process() { StartInfo = info }.Start();
        await Task.Delay(500);
        var builder = new CommandOptionsBuilder()
        .WithContinuousStreaming()
        .WithVflip()
        .WithHflip()
        .WithMJPEGVideoOptions(quality)
        .WithResolution(width, height);
        var args = builder.GetArguments();
        var process = new ProcessRunner(_videoProcessSettings);

        var pipe = new Pipe();
        logger.LogInformation("Starting Mjpeg stream with: {arguments}", args.Concat(" "));
        return (pipe, process.ContinuousRunAsync(args, pipe.Writer.AsStream(true)));
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