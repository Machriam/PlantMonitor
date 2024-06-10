using Plantmonitor.Server.Features.DeviceControl;

namespace Plantmonitor.Server.Features.DeviceConfiguration;

public interface IDeviceConnectionEventBus
{
    IEnumerable<DeviceHealthState> GetDeviceHealthInformation();

    void UpdateDeviceHealths(IEnumerable<DeviceHealthState> healths);
}

public class DeviceConnectionEventBus : IDeviceConnectionEventBus
{
    private static List<DeviceHealthState> s_deviceHealths = [];

    public void UpdateDeviceHealths(IEnumerable<DeviceHealthState> healths)
    {
        s_deviceHealths = healths.ToList();
    }

    public IEnumerable<DeviceHealthState> GetDeviceHealthInformation()
    {
#if DEBUG
        return s_deviceHealths.Append(new DeviceHealthState(new DeviceHealth("13be815a-cf95-4b58-b9f7-fd5d9f5431e9", "test", HealthState.NA), 0, "localhost:7006"));
#endif
        return s_deviceHealths;
    }
}
