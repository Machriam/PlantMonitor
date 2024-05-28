using PlantMonitorControl.Features.AppsettingsConfiguration;
using System.Diagnostics;

namespace PlantMonitorControl.Features.MeasureTemperature;

public interface IClick2TempInterop
{
    IEnumerable<string> GetDevices();

    string StartTemperatureReading(string[] devices);

    void StopRunning();
}

public class Click2TempInterop(IEnvironmentConfiguration configuration) : IClick2TempInterop
{
    private const string DeviceFoundText = "Found device: ";
    private static readonly string s_tempImagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tempImages_Click");
    private static bool s_isRunning = false;

    public void StopRunning()
    {
        s_isRunning = false;
    }

    public string StartTemperatureReading(string[] devices)
    {
        s_isRunning = true;
        if (Directory.Exists(s_tempImagePath)) Directory.Delete(s_tempImagePath, true);
        Directory.CreateDirectory(s_tempImagePath);
        async Task StartProcess()
        {
            await Task.Yield();
            while (s_isRunning)
            {
                var startInfo = new ProcessStartInfo()
                {
                    Arguments = $"{configuration.Temp2ClickPrograms.WriteThermalData} {devices.Concat(" ")} {s_tempImagePath}",
                    FileName = configuration.Temp2ClickPrograms.PythonExecutable
                };
                var process = new Process() { StartInfo = startInfo };
                process.Start();
                await Task.Delay(1000);
            }
        }
        _ = StartProcess();
        return s_tempImagePath;
    }

    public IEnumerable<string> GetDevices()
    {
        var result = new List<string>();
        void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data?.Contains(DeviceFoundText) == true)
            {
                result.Add(e.Data.Replace(DeviceFoundText, "").Trim());
            }
        }
        var startInfo = new ProcessStartInfo()
        {
            Arguments = $"{configuration.Temp2ClickPrograms.GetDevices}",
            FileName = configuration.Temp2ClickPrograms.PythonExecutable,
            UseShellExecute = false,
            RedirectStandardInput = true,
        };
        var process = new Process() { StartInfo = startInfo };
        process.OutputDataReceived += Process_OutputDataReceived;
        process.Start();
        process.BeginOutputReadLine();
        process.WaitForExit();
        return result;
    }
}
