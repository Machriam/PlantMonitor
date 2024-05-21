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
        return DeviceHealths.Append(new DeviceHealthState(new DeviceHealth() { DeviceId = "13be815a-cf95-4b58-b9f7-fd5d9f5431e9", DeviceName = "test", State = HealthState.NA }, 0, "localhost:7006"));
#endif
        return DeviceHealths;
    }
}
