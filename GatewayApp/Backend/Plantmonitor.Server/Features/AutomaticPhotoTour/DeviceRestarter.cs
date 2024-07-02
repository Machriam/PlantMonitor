using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Microsoft.EntityFrameworkCore;
using Plantmonitor.Server.Features.DeviceControl;

namespace Plantmonitor.Server.Features.AutomaticPhotoTour;

public interface IDeviceRestarter
{
    Task RestartDevice(string restartDeviceId, long photoTourId);
}

public class DeviceRestarter(IServiceScopeFactory scopeFactory) : IDeviceRestarter
{
    public async Task RestartDevice(string restartDeviceId, long photoTourId)
    {
        using var scope = scopeFactory.CreateScope();
        var eventBus = scope.ServiceProvider.GetRequiredService<IDeviceConnectionEventBus>();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var deviceApi = scope.ServiceProvider.GetRequiredService<IDeviceApiFactory>();
        var logEvent = dataContext.CreatePhotoTourEventLogger(photoTourId);
        if (!Guid.TryParse(restartDeviceId, out var deviceGuid))
        {
            logEvent($"Camera Device has no valid GUID: {restartDeviceId}", PhotoTourEventType.Error);
            return;
        }
        var switchData = dataContext.DeviceSwitchAssociations
            .Include(sw => sw.OutletOffFkNavigation)
            .Include(sw => sw.OutletOnFkNavigation)
            .FirstOrDefault(sw => sw.DeviceId == deviceGuid);
        if (switchData == null)
        {
            logEvent($"Automatic switching for {restartDeviceId} not possible. Camera device has no switch assigned!", PhotoTourEventType.Warning);
            return;
        }
        var switchingDevices = eventBus.GetDeviceHealthInformation()
            .Where(h => h.Health.State?.HasFlag(HealthState.CanSwitchOutlets) == true && h.Health.DeviceId != restartDeviceId);
        if (!switchingDevices.Any())
        {
            logEvent("No other devices found capable of switching", PhotoTourEventType.Warning);
            return;
        }
        foreach (var switchDevice in switchingDevices)
        {
            await deviceApi.SwitchOutletsClient(switchDevice.Ip).SwitchoutletAsync(switchData.OutletOffFkNavigation.Code);
            await Task.Delay(200);
        }
        eventBus.UpdateDeviceHealths(eventBus.GetDeviceHealthInformation().Where(d => d.Health.DeviceId != restartDeviceId));
        foreach (var switchDevice in switchingDevices)
        {
            await deviceApi.SwitchOutletsClient(switchDevice.Ip).SwitchoutletAsync(switchData.OutletOffFkNavigation.Code);
            await Task.Delay(200);
        }
        return;
    }
}
