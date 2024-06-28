using Iot.Device.Camera.Settings;
using Iot.Device.Common;
using System.Diagnostics;
using System.Globalization;

namespace PlantMonitorControl.Features.ImageTaking;

public interface ICameraInterop
{
    public const string VisCamera = nameof(VisCamera);
    public const string IrCamera = nameof(IrCamera);

    bool CameraIsRunning();

    Task<bool> CameraFound();

    Task<bool> CameraFunctional();

    Task<string> CameraInfo();

    Task<IResult> CaptureTestImage();

    Task KillImageTaking();

    Task<string> StreamPictureDataToFolder(float resolutionDivider, int quality, float distanceInM);

    Task CalibrateCamera();
}

public class RaspberryCameraInterop(IExposureSettingsEditor exposureSettings) : ICameraInterop
{
    private const int MaxWidth = 2304;
    private const int MaxHeight = 1296;
    private bool _cameraFound;
    private bool _deviceFunctional;
    private static bool s_cameraIsRunning;
    private static readonly string s_tempImagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tempImages_VIS");

    private readonly ProcessSettings _videoProcessSettings = new() { Filename = "rpicam-vid", WorkingDirectory = null };
    private readonly ProcessSettings _imageProcessSettings = new() { Filename = "rpicam-still", WorkingDirectory = null };

    public async Task<bool> CameraFunctional()
    {
        if (!_deviceFunctional) await CaptureTestImage();
        return _deviceFunctional;
    }

    public bool CameraIsRunning()
    {
        return s_cameraIsRunning;
    }

    public async Task<bool> CameraFound()
    {
        if (_cameraFound) return _cameraFound;
        var info = await CameraInfo();
        if (!info.Contains("no camera", StringComparison.InvariantCultureIgnoreCase) && info.Length > 10) _cameraFound = true;
        return _cameraFound;
    }

    public async Task KillImageTaking()
    {
        await new Process().RunProcess("pkill", $"-9 -f {_videoProcessSettings.Filename}");
        await new Process().RunProcess("pkill", $"-9 -f {_imageProcessSettings.Filename}");
        s_cameraIsRunning = false;
    }

    public async Task<string> StreamPictureDataToFolder(float resolutionDivider, int quality, float distanceInM)
    {
        if (distanceInM == 0) distanceInM = 0.01f;
        if (resolutionDivider == 0) resolutionDivider = 2;
        var exposure = exposureSettings.GetExposure();
        var focus = float.Round(1f / distanceInM, 2);
        var width = (int)(MaxWidth / resolutionDivider);
        var height = (int)(MaxHeight / resolutionDivider);
        await KillImageTaking();
        if (Path.Exists(s_tempImagePath)) Directory.Delete(s_tempImagePath, true);
        Directory.CreateDirectory(s_tempImagePath);
        var filePath = Path.Combine(s_tempImagePath, "%06d.jpg");
        new Process().RunProcess(
            "rpicam-vid", $"-t 0 -v 0 --width {width} --height {height} --mode 4608:2592 " +
            $"--gain {exposure.Gain.ToString("0.00", CultureInfo.InvariantCulture)} --shutter {exposure.ExposureTimeInMicroSeconds.ToString("0", CultureInfo.InvariantCulture)} " +
            $"--framerate {(resolutionDivider == 1 ? 4 : -1)} --codec mjpeg -q {quality} --hflip --vflip --segment 1 --lens-position {focus} -o {filePath} ")
            .RunInBackground(ex => ex.LogError("RPI-Cam error"));
        s_cameraIsRunning = true;
        return s_tempImagePath;
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Device only runs Linux")]
    public async Task CalibrateCamera()
    {
        await KillImageTaking();
        s_cameraIsRunning = true;
        var rpicamHello = $"{s_tempImagePath}/rpihello.sh";
        var resultFile = $"{s_tempImagePath}/out.txt";
        File.WriteAllText(rpicamHello, $"#/bin/bash\nrpicam-hello -t 1sec &>{resultFile}");
        File.SetUnixFileMode(rpicamHello, UnixFileMode.OtherExecute | UnixFileMode.GroupExecute | UnixFileMode.UserExecute);
        await new Process().RunProcess("/bin/bash", rpicamHello);
        var output = File.ReadAllText(resultFile);
        File.Delete(rpicamHello);
        File.Delete(resultFile);
        s_cameraIsRunning = false;
        var exposure = exposureSettings.GetExposureFromStdout(output);
        exposureSettings.UpdateExposure(exposure);
    }
}
