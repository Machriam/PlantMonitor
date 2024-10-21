using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.DeviceControl;

namespace Plantmonitor.Server.Features.DeviceConfiguration;

public interface IDeviceConnectionEventBus
{
    IEnumerable<DeviceHealthState> GetAllSeenDevices();

    IEnumerable<DeviceHealthState> GetDeviceHealthInformation();

    void UpdateDeviceHealths(IEnumerable<DeviceHealthState> healths);
}

public class DeviceConnectionEventBus(IServiceScopeFactory scopeFactory) : IDeviceConnectionEventBus
{
    private static List<DeviceHealthState> s_deviceHealths = [];

    public void UpdateDeviceHealths(IEnumerable<DeviceHealthState> healths)
    {
        s_deviceHealths = healths.ToList();
    }

    public IEnumerable<DeviceHealthState> GetDeviceHealthInformation()
    {
#if DEBUG
        return s_deviceHealths.Append(new DeviceHealthState(new DeviceHealth(new(129, 39), "13be815a-cf95-4b58-b9f7-fd5d9f5431e9", "test", HealthState.NA), 0, "localhost:7006"));
#endif
        return s_deviceHealths;
    }

    public IEnumerable<DeviceHealthState> GetAllSeenDevices()
    {
        using var scope = scopeFactory.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var storedDevices = dataContext.ConfigurationData.GetValue(ConfigurationDatumKeys.AllSeenDevices);
        var onlineDevices = GetDeviceHealthInformation();
        foreach (var device in onlineDevices) yield return device;
        if (storedDevices.IsEmpty()) yield break;
        var knownDevicesById = storedDevices.FromJson<List<DeviceHealthState>>() ?? [];
        var onlineDeviceIds = onlineDevices.Select(pd => pd.Health.DeviceId).ToHashSet();
        foreach (var device in knownDevicesById.Where(kd => !onlineDeviceIds.Contains(kd.Health.DeviceId)))
        {
            yield return new DeviceHealthState(device.Health, 999, "");
        }
    }
}
