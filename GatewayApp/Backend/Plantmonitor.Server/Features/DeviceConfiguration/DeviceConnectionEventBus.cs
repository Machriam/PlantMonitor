using Microsoft.AspNetCore.Mvc.Infrastructure;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.DeviceControl;

namespace Plantmonitor.Server.Features.DeviceConfiguration;

public interface IDeviceConnectionEventBus
{
    IEnumerable<DeviceHealthState> GetAllSeenHealths();

    IEnumerable<DeviceHealthState> GetDeviceHealthInformation();

    void RemoveSeenDevice(string ip);

    void UpdateDeviceHealths(IEnumerable<DeviceHealthState> healths);
}

public class DeviceConnectionEventBus(IServiceScopeFactory scopeFactory) : IDeviceConnectionEventBus
{
    private const string AllSeenDeviceHealthsKey = nameof(AllSeenDeviceHealthsKey);
    private static List<DeviceHealthState> s_currentDeviceHealths = [];
    private static List<DeviceHealthState> s_allSeenDeviceHealths = [];
    private static bool s_seenDevicesInitialized;

    public void UpdateDeviceHealths(IEnumerable<DeviceHealthState> healths)
    {
        InitializeSeenDevices();
        s_currentDeviceHealths = healths.ToList();
        var seenDeviceChanged = false;
        foreach (var health in healths)
        {
            if (health.RetryTimes == 0)
            {
                s_allSeenDeviceHealths.Remove(s_allSeenDeviceHealths.Find(bdh => bdh.Ip == health.Ip));
                s_allSeenDeviceHealths.Add(health);
                seenDeviceChanged = true;
            }
        }
        if (!seenDeviceChanged) return;
        using var scope = scopeFactory.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var existingItem = dataContext.ConfigurationData.FirstOrDefault(cd => cd.Key == AllSeenDeviceHealthsKey);
        if (existingItem == null)
        {
            dataContext.ConfigurationData.Add(new ConfigurationDatum()
            {
                Key = AllSeenDeviceHealthsKey,
                Value = s_allSeenDeviceHealths.AsJson()
            });
            dataContext.SaveChanges();
            return;
        }
        existingItem.Value = s_allSeenDeviceHealths.AsJson();
        dataContext.SaveChanges();
    }

    private void InitializeSeenDevices()
    {
        if (s_seenDevicesInitialized) return;
        using var scope = scopeFactory.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        s_allSeenDeviceHealths = dataContext.ConfigurationData
            .FirstOrDefault(cd => cd.Key == AllSeenDeviceHealthsKey)?.Value
            .FromJson<List<DeviceHealthState>>() ?? [];
        s_seenDevicesInitialized = true;
    }

    public IEnumerable<DeviceHealthState> GetAllSeenHealths()
    {
        return s_allSeenDeviceHealths ?? [];
    }

    public void RemoveSeenDevice(string ip)
    {
        s_allSeenDeviceHealths.Remove(s_allSeenDeviceHealths.Find(dh => dh.Ip == ip));
    }

    public IEnumerable<DeviceHealthState> GetDeviceHealthInformation()
    {
#if DEBUG
        return s_deviceHealths.Append(new DeviceHealthState(new DeviceHealth(new(129, 39), "13be815a-cf95-4b58-b9f7-fd5d9f5431e9", "test", HealthState.NA), 0, "localhost:7006"));
#endif
        return s_currentDeviceHealths;
    }
}
