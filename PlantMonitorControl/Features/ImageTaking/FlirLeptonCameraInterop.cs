using System.Diagnostics;
using PlantMonitorControl.Features.AppsettingsConfiguration;

namespace PlantMonitorControl.Features.ImageTaking;

public class FlirLeptonCameraInterop(IEnvironmentConfiguration configuration) : ICameraInterop
{
    private static readonly string s_tempImagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tempImages_IR");
    private static bool s_cameraFound;
    private static bool s_cameraIsRunning;
    private static Process? s_streamProcess;

    public Task<bool> CameraFound()
    {
        return Task.FromResult(s_cameraFound);
    }

    public async Task<bool> CameraFunctional()
    {
        await CaptureTestImage();
        return Directory.GetFiles(s_tempImagePath).Length > 0;
    }

    public async Task<string> CameraInfo()
    {
        await KillImageTaking();
        return await new Process().GetProcessStdout(configuration.IRPrograms.DeviceHealth, s_tempImagePath);
    }

    public bool CameraIsRunning()
    {
        return s_cameraIsRunning;
    }

    public async Task<IResult> CaptureTestImage()
    {
        await KillImageTaking();
        InitializeFolder();
        await new Process().RunProcess(configuration.IRPrograms.CaptureImage, s_tempImagePath);
        await Task.Delay(100);
        var files = Directory.GetFiles(s_tempImagePath);
        var bytes = files.FirstOrDefault()?.GetBytesFromIrFilePath(out _) ?? [];
        if (bytes.Length > 0) s_cameraFound = true;
        return Results.File(bytes, "image/raw");
    }

    private static void InitializeFolder()
    {
        if (Path.Exists(s_tempImagePath)) Directory.Delete(s_tempImagePath, true);
        Directory.CreateDirectory(s_tempImagePath);
    }

    public async Task KillImageTaking()
    {
        await new Process().RunProcess("pkill", $"-USR2 {Path.GetFileName(configuration.IRPrograms.StreamData)}");
        s_cameraIsRunning = false;
    }

    public void RequestFFC()
    {
        if (s_streamProcess == null) return;
        s_streamProcess.SendSignal(ProcessExtensions.Signum.SIGUSR1);
    }

    public async Task<string> StreamPictureDataToFolder(float resolutionDivider, int quality, float distanceInM)
    {
        await KillImageTaking();
        InitializeFolder();
        s_streamProcess = new Process();
        s_streamProcess.RunProcess(configuration.IRPrograms.StreamData, s_tempImagePath)
            .RunInBackground(ex => ex.LogError());
        s_cameraIsRunning = true;
        return s_tempImagePath;
    }
}
