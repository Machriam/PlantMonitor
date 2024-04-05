using Iot.Device.Camera;
using Iot.Device.Camera.Settings;
using Iot.Device.Common;
using Plantmonitor.Shared.Extensions;

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
        var test = await new Capture(_videoProcessSettings).CaptureVideo();
        return Results.File(test, "video/mp4");
        var builder = new CommandOptionsBuilder()
        .WithContinuousStreaming()
        .WithVflip()
        .WithHflip()
        .WithH264VideoOptions("baseline", "4", 15)
        .WithResolution(640, 480);
        var args = builder.GetArguments();
        using var process = new ProcessRunner(_videoProcessSettings);

        using var file = File.Create("h264");
        var ms = new MemoryStream();
        var task = await process.ContinuousRunAsync(args, file);
        await Task.Delay(2000);
        process.Dispose();
        try { await task; } catch (Exception ex) { Console.WriteLine(ex.Message); }
        ms.Position = 0;
        file.Flush();
        file.Position = 0;
        file.CopyTo(ms);
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

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

internal class Capture
{
    private readonly ProcessSettings _processSettings;

    public Capture(ProcessSettings processSettings)
    {
        _processSettings = processSettings;
    }

    private string CreateFilename(string extension)
    {
        var now = DateTime.Now;
        var path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var filename = $"{now.Year:00}{now.Month:00}{now.Day:00}_{now.Hour:00}{now.Minute:00}{now.Second:00}{now.Millisecond:0000}.{extension}";
        return Path.Combine(path, filename);
    }

    public async Task<string> CaptureStill()
    {
        var builder = new CommandOptionsBuilder()
            .WithTimeout(1)
            .WithVflip()
            .WithHflip()
            .WithPictureOptions(90, "jpg")
            .WithResolution(640, 480);
        var args = builder.GetArguments();

        using var proc = new ProcessRunner(_processSettings);
        Console.WriteLine("Using the following command line:");
        Console.WriteLine(proc.GetFullCommandLine(args));
        Console.WriteLine();

        var filename = CreateFilename("jpg");
        using var file = File.OpenWrite(filename);
        await proc.ExecuteAsync(args, file);
        return filename;
    }

    public async Task<string> CaptureVideo()
    {
        var builder = new CommandOptionsBuilder()
            .WithContinuousStreaming(0)
            .WithVflip()
            .WithHflip()
            .WithResolution(640, 480);
        var args = builder.GetArguments();

        using var proc = new ProcessRunner(_processSettings);
        Console.WriteLine("Using the following command line:");
        Console.WriteLine(proc.GetFullCommandLine(args));
        Console.WriteLine();

        var filename = CreateFilename("h264");
        using var file = File.OpenWrite(filename);

        /*
        The following code will stop capturing after 5 seconds
        We could do the same specifying ".WithContinuousStreaming(5000)" instead of 0
        But the following code shows how to stop the capture programmatically
        */

        // The ContinuousRunAsync method offload the capture on a separate thread
        var task = await proc.ContinuousRunAsync(args, file);
        await Task.Delay(5000);
        proc.Dispose();
        // The following try/catch is needed to trash the OperationCanceledException triggered by the Dispose
        try
        {
            await task;
        }
        catch (Exception)
        {
        }

        return filename;
    }

    public async Task CaptureTimelapse()
    {
        // The false argument avoids the app to output to stdio
        // Time lapse images will be directly saved on disk without
        // writing anything on the terminal
        // Alternatively, we can leave the default (true) and
        // use the '.Remove' method
        var builder = new CommandOptionsBuilder(false)
            // .Remove(CommandOptionsBuilder.Get(Command.Output))
            .WithOutput("image_%04d.jpg")
            .WithTimeout(5000)
            .WithTimelapse(1000)
            .WithVflip()
            .WithHflip()
            .WithResolution(640, 480);
        var args = builder.GetArguments();

        using var proc = new ProcessRunner(_processSettings);
        Console.WriteLine("Using the following command line:");
        Console.WriteLine(proc.GetFullCommandLine(args));
        Console.WriteLine();

        // The ContinuousRunAsync method offload the capture on a separate thread
        // the first await is tied the thread being run
        // the second await is tied to the capture
        var task = await proc.ContinuousRunAsync(args, default(Stream));
        await task;
    }

    public async Task<IEnumerable<CameraInfo>> List()
    {
        var builder = new CommandOptionsBuilder()
            .WithListCameras();
        var args = builder.GetArguments();

        using var proc = new ProcessRunner(_processSettings);
        Console.WriteLine("Using the following command line:");
        Console.WriteLine(proc.GetFullCommandLine(args));
        Console.WriteLine();

        var text = await proc.ExecuteReadOutputAsStringAsync(args);
        Console.WriteLine($"Output being parsed:");
        Console.WriteLine(text);
        Console.WriteLine();

        var cameras = await CameraInfo.From(text);
        return cameras;
    }
}