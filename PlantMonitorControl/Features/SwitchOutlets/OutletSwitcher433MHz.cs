using System.Diagnostics;
using PlantMonitorControl.Features.AppsettingsConfiguration;

namespace PlantMonitorControl.Features.SwitchOutlets;

public interface IOutletSwitcher
{
    Task<bool> DeviceCanSwitchOutlets();

    void SwitchOutlet(long code);
}

public class OutletSwitcher433MHz(IEnvironmentConfiguration configuration) : IOutletSwitcher
{
    private const int TestPayload = 12345;

    public async Task<bool> DeviceCanSwitchOutlets()
    {
        var startInfo = new ProcessStartInfo()
        {
            FileName = configuration.PowerSwitchPrograms.ReceiveTest,
            Arguments = $"{configuration.PowerSwitchPinout.WiringPiTXIN}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        var success = false;
        void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            success = e.Data?.Trim() == TestPayload.ToString();
        }
        var process = new Process() { StartInfo = startInfo };
        process.OutputDataReceived += Process_OutputDataReceived;
        process.Start();
        process.BeginOutputReadLine();
        await Task.Yield();
        SwitchOutlet(TestPayload);
        await Task.Delay(200);
        if (!process.HasExited) process.Kill();
        return success;
    }

    public void SwitchOutlet(long code)
    {
        var startInfo = new ProcessStartInfo()
        {
            FileName = configuration.PowerSwitchPrograms.SwitchOutlet,
            Arguments = $"{configuration.PowerSwitchPinout.WiringPiTX} {code}",
        };
        var process = new Process() { StartInfo = startInfo };
        process.Start();
        process.WaitForExit();
    }
}
