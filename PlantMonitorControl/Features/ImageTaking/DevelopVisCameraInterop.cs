namespace PlantMonitorControl.Features.ImageTaking;

public class DevelopVisCameraInterop() : ICameraInterop
{
    private const string DataFolder = "../PlantMonitorControl.Tests/TestData/VisData";
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
        return Task.FromResult("Dev VIS Cam");
    }

    public bool CameraIsRunning()
    {
        return s_isRunning;
    }

    public async Task<IResult> CaptureTestImage()
    {
        var file = Directory.GetFiles(DataFolder);
        var bytes = File.ReadAllBytes(file[0]);
        await Task.Yield();
        return Results.File(bytes, "image/jpg");
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
        const string CopyToFolder = "./" + nameof(DevelopVisCameraInterop);
        if (Path.Exists(CopyToFolder)) Directory.Delete(CopyToFolder, true);
        var copyToDir = Directory.CreateDirectory(CopyToFolder).FullName;
        var counter = 0;
        await Task.Yield();
        async Task CopyTask()
        {
            while (s_isRunning)
            {
                foreach (var file in files)
                {
                    await Task.Delay(20);
                    File.Copy(file, Path.Combine(copyToDir, counter++.ToString(FileStreamingReader.CounterFormat) + ".jpg"));
                }
            }
        }
        CopyTask().RunInBackground(ex => ex.LogError());
        return copyToDir;
    }
}
