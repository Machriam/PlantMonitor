namespace PlantMonitorControl.Features.ImageTaking;

public class DevelopIrCameraInterop() : ICameraInterop
{
    private const string IrDataFolder = "../PlantMonitorControl.Tests/TestData/IRData";
    private static bool s_isRunning;

    public Task<bool> CameraFound()
    {
        return Task.FromResult(true);
    }

    public Task<bool> CameraFunctional()
    {
        return Task.FromResult(true);
    }

    public Task<string> CameraInfo()
    {
        return Task.FromResult("Dev IR Cam");
    }

    public bool CameraIsRunning()
    {
        return s_isRunning;
    }

    public async Task<IResult> CaptureTestImage()
    {
        var file = Directory.GetFiles(IrDataFolder);
        var bytes = File.ReadAllBytes(file[0]);
        await Task.Yield();
        return Results.File(bytes, "image/raw");
    }

    public Task KillImageTaking()
    {
        s_isRunning = false;
        return Task.CompletedTask;
    }

    public async Task<string> StreamPictureDataToFolder(float resolutionDivider, int quality, float distanceInM)
    {
        s_isRunning = true;
        var files = Directory.GetFiles(IrDataFolder);
        var copyToDir = Directory.CreateDirectory("./" + nameof(DevelopIrCameraInterop)).FullName;
        var counter = 0;
        while (s_isRunning)
        {
            foreach (var file in files)
            {
                await Task.Delay(20);
                File.Copy(file, Path.Combine(copyToDir, counter++.ToString($"{FileStreamingReader.CounterFormat}.rawir")));
            }
        }
        return copyToDir;
    }
}
