using Serilog;

namespace PlantMonitorControl.Features.SwitchOutlets;

public class OutletSwitcherDevelop() : IOutletSwitcher
{
    public Task<bool> DeviceCanSwitchOutlets()
    {
        return Task.FromResult(true);
    }

    public void SwitchOutlet(long code)
    {
        Log.Information("Switched outlet code: {code}", code);
    }
}
