using Plantmonitor.Shared.Features.HealthChecking;

namespace Plantmonitor.Server.Features.DeviceConfiguration;

public interface IDeviceConnectionEventBus
{
    IEnumerable<DeviceHealthState> GetDeviceHealthInformation();

    void UpdateDeviceHealths(IEnumerable<DeviceHealthState> healths);
}

public class DeviceConnectionEventBus : IDeviceConnectionEventBus
{
    private static List<DeviceHealthState> DeviceHealths = [];

    public void UpdateDeviceHealths(IEnumerable<DeviceHealthState> healths)
    {
        DeviceHealths = healths.ToList();
    }

    public IEnumerable<DeviceHealthState> GetDeviceHealthInformation()
    {
#if DEBUG
        return DeviceHealths.Append(new DeviceHealthState(new DeviceHealth() { DeviceId = "test-id", DeviceName = "test", State = HealthState.NA }, 0, "localhost:7006"));
#endif
        return DeviceHealths;
    }
}
