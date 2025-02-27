﻿using System.Diagnostics;
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
    private bool _canSwitchOutlets;

    public async Task<bool> DeviceCanSwitchOutlets()
    {
        if (_canSwitchOutlets) return true;
        var startInfo = new ProcessStartInfo()
        {
            FileName = configuration.PowerSwitchPrograms.ReceiveTest,
            Arguments = $"{configuration.PowerSwitchPinout.WiringPiTXIN}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        var success = false;
        var waitingForData = false;
        async Task Timeout() { await Task.Delay(2000); waitingForData = true; }
        Timeout().RunInBackground(ex => ex.LogError());
        void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data?.Trim() == "Waiting for data") waitingForData = true;
            if (e.Data?.Trim() == TestPayload.ToString()) success = true;
        }
        var process = new Process() { StartInfo = startInfo };
        process.OutputDataReceived += Process_OutputDataReceived;
        process.Start();
        process.BeginOutputReadLine();
        while (!waitingForData) await Task.Delay(200);
        SwitchOutlet(TestPayload);
        await Task.Delay(200);
        if (!process.HasExited) process.Kill();
        _canSwitchOutlets = success;
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
