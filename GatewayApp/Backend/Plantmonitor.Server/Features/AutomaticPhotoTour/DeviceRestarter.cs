using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Microsoft.EntityFrameworkCore;
using Plantmonitor.Server.Features.DeviceControl;
using System.Collections.Concurrent;
using Serilog;

namespace Plantmonitor.Server.Features.AutomaticPhotoTour;

public interface IDeviceRestarter
{
    Task RestartDevice(string restartDeviceId, long? photoTourId);

    Task<(bool? DeviceHealthy, DeviceHealthState ImagingDevice)> CheckDeviceHealth(long photoTourId, IServiceScope scope, IDataContext dataContext);
}

public class DeviceRestarter(IServiceScopeFactory scopeFactory) : IDeviceRestarter
{
    private static readonly ConcurrentDictionary<Guid, DateTime> s_lastRestarts = [];

    public async Task<(bool? DeviceHealthy, DeviceHealthState ImagingDevice)> CheckDeviceHealth(long photoTourId, IServiceScope scope, IDataContext dataContext)
    {
        var eventBus = scope.ServiceProvider.GetRequiredService<IDeviceConnectionStorage>();
        var deviceApi = scope.ServiceProvider.GetRequiredService<IDeviceApiFactory>();
        var photoTourData = dataContext.AutomaticPhotoTours
            .Include(apt => apt.TemperatureMeasurements)
            .FirstOrDefault(apt => apt.Id == photoTourId);
        if (photoTourData == default)
        {
            Log.Logger.Log("Phototour not found", PhotoTourEventType.Error);
            return (false, default);
        }
        var hasIrCamera = photoTourData.TemperatureMeasurements.Any(tm => tm.IsThermalCamera());
        var logEvent = dataContext.CreatePhotoTourEventLogger(photoTourId);
        var deviceHealth = eventBus.GetCurrentDeviceHealths()
            .FirstOrDefault(h => h.Health.DeviceId == photoTourData.DeviceId.ToString());
        if (deviceHealth == default)
        {
            logEvent($"Camera Device {photoTourData.DeviceId} not found. Trying Restart.", PhotoTourEventType.Error);
            RestartDevice(photoTourData.DeviceId.ToString(), photoTourId).RunInBackground(ex => ex.LogError());
            return (null, deviceHealth);
        }
        logEvent($"Checking Camera {photoTourData.DeviceId}", PhotoTourEventType.Information);
        var irTest = hasIrCamera ? await deviceApi.IrImageTakingClient(deviceHealth.Ip).PreviewimageAsync() : default;
        var irImage = irTest?.Stream.ConvertToArray() ?? [];
        var visTest = await deviceApi.VisImageTakingClient(deviceHealth.Ip).PreviewimageAsync();
        var visImage = visTest.Stream.ConvertToArray();
        if ((hasIrCamera && irImage.Length < 100) || visImage.Length < 100)
        {
            var notWorkingCameras = new List<string>()
                .PushIf("IR", _ => irImage.Length < 100)
                .PushIf("VIS", _ => visImage.Length < 100)
                .Concat(", ");
            logEvent($"Camera {notWorkingCameras} not working. Trying Restart.", PhotoTourEventType.Error);
            RestartDevice(photoTourData.DeviceId.ToString(), photoTourId).RunInBackground(ex => ex.LogError());
            return (null, deviceHealth);
        }
        logEvent($"Camera working {photoTourData.DeviceId}", PhotoTourEventType.Information);

        return (true, deviceHealth);
    }

    public async Task RestartDevice(string restartDeviceId, long? photoTourId)
    {
        using var scope = scopeFactory.CreateScope();
        var eventBus = scope.ServiceProvider.GetRequiredService<IDeviceConnectionStorage>();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var deviceApi = scope.ServiceProvider.GetRequiredService<IDeviceApiFactory>();
        var logEvent = photoTourId == null ? Log.Logger.Log : dataContext.CreatePhotoTourEventLogger(photoTourId.Value);
        if (!Guid.TryParse(restartDeviceId, out var deviceGuid))
        {
            logEvent($"Camera Device has no valid GUID: {restartDeviceId}", PhotoTourEventType.Error);
            return;
        }
        if (s_lastRestarts.TryGetValue(deviceGuid, out var lastRestart) && (DateTime.UtcNow - lastRestart).TotalMinutes < 5)
        {
            logEvent($"Restart is at most possible every 5 minutes: {restartDeviceId}", PhotoTourEventType.Information);
            return;
        }
        var switchData = dataContext.DeviceSwitchAssociations
            .Include(sw => sw.OutletOffFkNavigation)
            .Include(sw => sw.OutletOnFkNavigation)
            .FirstOrDefault(sw => sw.DeviceId == deviceGuid);
        if (switchData == null)
        {
            s_lastRestarts.AddOrUpdate(deviceGuid, DateTime.UtcNow, (_1, _2) => DateTime.UtcNow);
            logEvent($"Automatic switching for {restartDeviceId} not possible. Camera device has no switch assigned!", PhotoTourEventType.Warning);
            return;
        }
        var switchingDevices = eventBus.GetCurrentDeviceHealths()
            .Where(h => h.Health.State?.HasFlag(HealthState.CanSwitchOutlets) == true && h.Health.DeviceId != restartDeviceId);
        if (!switchingDevices.Any())
        {
            s_lastRestarts.AddOrUpdate(deviceGuid, DateTime.UtcNow, (_1, _2) => DateTime.UtcNow);
            logEvent("No other devices found capable of switching", PhotoTourEventType.Warning);
            return;
        }
        foreach (var switchDevice in switchingDevices)
        {
            await deviceApi.SwitchOutletsClient(switchDevice.Ip).SwitchoutletAsync(switchData.OutletOffFkNavigation.Code);
            await Task.Delay(200);
        }
        eventBus.UpdateDeviceHealths(eventBus.GetCurrentDeviceHealths().Where(d => d.Health.DeviceId != restartDeviceId));
        s_lastRestarts.AddOrUpdate(deviceGuid, DateTime.UtcNow, (_1, _2) => DateTime.UtcNow);
        foreach (var switchDevice in switchingDevices)
        {
            await deviceApi.SwitchOutletsClient(switchDevice.Ip).SwitchoutletAsync(switchData.OutletOnFkNavigation.Code);
            await Task.Delay(200);
        }
        return;
    }
}
