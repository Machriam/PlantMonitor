namespace PlantMonitorControl.Features.MeasureTemperature;

public class DevelopClick2TempInterop : IClick2TempInterop
{
    private const string CopyToFolder = "./" + nameof(DevelopClick2TempInterop);
    private static bool s_isRunning;

    public IEnumerable<string> GetDevices()
    {
        return ["0xff", "0x00"];
    }

    public string StartTemperatureReading(string[] devices)
    {
        s_isRunning = true;
        var counter = 0;
        if (Path.Exists(CopyToFolder)) Directory.Delete(CopyToFolder, true);
        Directory.CreateDirectory(CopyToFolder);
        async Task CopyTask()
        {
            while (s_isRunning)
            {
                await Task.Delay(Random.Shared.Next(200, 1000));
                File.WriteAllText($"{CopyToFolder}/{counter++:000000}.rawtemp", devices.Select(d => $"{d}: {Random.Shared.NextSingle() * 30f}").Concat("\n"));
            }
        }
        _ = CopyTask();
        return CopyToFolder;
    }

    public void StopRunning()
    {
        s_isRunning = false;
    }
}
