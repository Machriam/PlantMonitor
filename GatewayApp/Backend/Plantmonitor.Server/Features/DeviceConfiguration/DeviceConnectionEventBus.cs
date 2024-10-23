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
    private readonly Timer _updateStoredDevices = new(_ => UpdateStoredDevices(scopeFactory), null, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1));

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
        var storedDevices = GetStoredDevices(scopeFactory);
        var onlineDevices = GetDeviceHealthInformation();
        foreach (var device in onlineDevices) yield return device;
        var onlineDeviceIds = onlineDevices.Where(pd => pd.Health?.DeviceId != default)
            .Select(pd => pd.Health.DeviceId).ToHashSet();
        foreach (var device in storedDevices.Where(kd => !onlineDeviceIds.Contains(kd.Health.DeviceId)))
        {
            yield return new DeviceHealthState(device.Health, 999, "");
        }
    }

    private static void UpdateStoredDevices(IServiceScopeFactory scopeFactory)
    {
        var storedDevices = GetStoredDevices(scopeFactory);
        var onlineIds = s_deviceHealths.Where(dh => dh.Health?.DeviceId != default).Select(dh => dh.Health.DeviceId).ToHashSet();
        var newJson = storedDevices
            .Where(sd => !onlineIds.Contains(sd.Health.DeviceId))
            .Pipe(sd => Enumerable.Concat(sd, s_deviceHealths))
            .AsJson();
        using var scope = scopeFactory.CreateScope();
        using var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        dataContext.ConfigurationData.SetValue(ConfigurationDatumKeys.AllSeenDevices, newJson);
        dataContext.SaveChanges();
    }

    private static List<DeviceHealthState> GetStoredDevices(IServiceScopeFactory scopeFactory)
    {
        using var scope = scopeFactory.CreateScope();
        using var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var storedDevices = dataContext.ConfigurationData.GetValue(ConfigurationDatumKeys.AllSeenDevices);
        if (storedDevices.IsEmpty()) return [];
        return storedDevices.FromJson<List<DeviceHealthState>>() ?? [];
    }
}
