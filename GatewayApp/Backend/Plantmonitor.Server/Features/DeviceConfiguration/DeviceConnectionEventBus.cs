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
        return DeviceHealths;
    }
}