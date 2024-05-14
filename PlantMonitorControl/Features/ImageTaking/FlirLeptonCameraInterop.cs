using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using PlantMonitorControl.Features.AppsettingsConfiguration;

namespace PlantMonitorControl.Features.MotorMovement;

public class FlirLeptonCameraInterop(IEnvironmentConfiguration configuration) : ICameraInterop
{
    private static readonly string s_tempImagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tempImages_IR");
    private static bool s_cameraFound;
    private static bool s_cameraIsRunning;

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
        var captureProcess = new ProcessStartInfo(configuration.IRPrograms.DeviceHealth, s_tempImagePath)
        {
            RedirectStandardOutput = true
        };
        var process = new Process() { StartInfo = captureProcess };
        StringBuilder result = new();
        process.OutputDataReceived += (sender, args) => result.AppendLine(args.Data);
        process.BeginOutputReadLine();
        process.Start();
        await process.WaitForExitAsync();
        return result.ToString();
    }

    public bool CameraIsRunning()
    {
        return s_cameraIsRunning;
    }

    public async Task<IResult> CaptureTestImage()
    {
        await KillImageTaking();
        InitializeFolder();
        var captureProcess = new ProcessStartInfo(configuration.IRPrograms.CaptureImage, s_tempImagePath);
        new Process() { StartInfo = captureProcess }.Start();
        while (Directory.GetFiles(s_tempImagePath).Length == 0)
        {
            await Task.Delay(200);
        }
        var files = Directory.GetFiles(s_tempImagePath);
        var bytes = File.ReadAllText(files[0])
            .Split(" ")
            .SelectMany(x => BitConverter.GetBytes(int.Parse(x)))
            .ToArray();
        if (bytes.Length > 0) s_cameraFound = true;
        return Results.File(bytes, "image/raw");
    }

    private static void InitializeFolder()
    {
        if (Path.Exists(s_tempImagePath)) Directory.Delete(s_tempImagePath, true);
        Directory.CreateDirectory(s_tempImagePath);
    }

    public Task KillImageTaking()
    {
        var killVideo = new ProcessStartInfo("pkill", $"-USR2 {Path.GetFileName(configuration.IRPrograms.StreamData)}");
        new Process() { StartInfo = killVideo }.Start();
        s_cameraIsRunning = false;
        return Task.CompletedTask;
    }

    public async Task<string> StreamPictureDataToFolder(float resolutionDivider, int quality, float distanceInM)
    {
        await KillImageTaking();
        InitializeFolder();
        var captureProcess = new ProcessStartInfo(configuration.IRPrograms.StreamData, s_tempImagePath);
        new Process() { StartInfo = captureProcess }.Start();
        s_cameraIsRunning = true;
        return s_tempImagePath;
    }
}
