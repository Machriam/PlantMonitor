namespace PlantMonitorControl.Features.ImageTaking;

public class DevelopIrCameraInterop() : ICameraInterop
{
    private const string DataFolder = "../PlantMonitorControl.Tests/TestData/IRData";
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
        var files = Directory.GetFiles(DataFolder);
        var bytes = files[Random.Shared.Next(0, files.Length)].GetBytesFromIrFilePath(out _);
        await Task.Yield();
        return Results.File(bytes.Bytes, "image/raw");
    }

    public Task KillImageTaking()
    {
        s_isRunning = false;
        return Task.CompletedTask;
    }

    public Task CalibrateCamera()
    {
        return Task.CompletedTask;
    }

    public async Task<string> StreamPictureDataToFolder(float resolutionDivider, int quality, float distanceInM)
    {
        s_isRunning = true;
        var files = Directory.GetFiles(DataFolder);
        const string CopyToFolder = "./" + nameof(DevelopIrCameraInterop);
        if (Path.Exists(CopyToFolder)) Directory.Delete(CopyToFolder, true);
        var copyToDir = Directory.CreateDirectory(CopyToFolder).FullName;
        var counter = 0;
        await Task.Yield();
        async Task CopyFiles()
        {
            while (s_isRunning)
            {
                foreach (var file in files)
                {
                    await Task.Delay(500);
                    var _ = file.GetBytesFromIrFilePath(out var temperatureInK);
                    File.Copy(file, Path.Combine(copyToDir, $"{counter++.ToString(FileStreamingReader.CounterFormat)}_{temperatureInK}.rawir"));
                }
            }
        }
        CopyFiles().RunInBackground(ex => ex.LogError());
        return copyToDir;
    }

    public IEnumerable<DateTime> LastCalibrationTimes() => [.. Enumerable.Range(0, 12).Select(x => DateTime.UtcNow.AddSeconds(-x * 5))];
}
