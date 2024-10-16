using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Microsoft.EntityFrameworkCore;
using Plantmonitor.Server.Features.DeviceControl;
using System.Collections.Concurrent;
using Serilog;
using static Plantmonitor.Server.Features.AutomaticPhotoTour.DeviceRestarter;

namespace Plantmonitor.Server.Features.AutomaticPhotoTour;

public interface IDeviceRestarter
{
    Task RequestRestartDevice(string restartDeviceId, long? photoTourId, string deviceName);

    Task ImmediateRestartDevice(string restartDeviceId, long? photoTourId, string deviceName);

    Task<DeviceHealthResult> CheckDeviceHealth(long photoTourId, IServiceScope scope, IDataContext dataContext);
}

public class DeviceRestarter(IServiceScopeFactory scopeFactory) : IDeviceRestarter
{
    public record struct DeviceHealthResult(bool? DeviceHealthy, DeviceHealthState ImagingDevice, bool HasIr);
    private static readonly ConcurrentDictionary<Guid, DateTime> s_lastRestarts = [];
    private static readonly ConcurrentDictionary<Guid, int> s_restartRequested = [];
    private const int FailureThreshold = 2;
    private const int SafetyWaitBeforeRestart = 10000;

    public async Task<DeviceHealthResult> CheckDeviceHealth(long photoTourId, IServiceScope scope, IDataContext dataContext)
    {
        var eventBus = scope.ServiceProvider.GetRequiredService<IDeviceConnectionEventBus>();
        var deviceApi = scope.ServiceProvider.GetRequiredService<IDeviceApiFactory>();
        var photoTourData = dataContext.AutomaticPhotoTours
            .Include(apt => apt.TemperatureMeasurements)
            .FirstOrDefault(apt => apt.Id == photoTourId);
        if (photoTourData == default)
        {
            Log.Logger.Log("Phototour not found", PhotoTourEventType.Error);
            return new(false, default, false);
        }
        var hasIrCamera = photoTourData.TemperatureMeasurements.Any(tm => tm.IsThermalCamera());
        var logEvent = dataContext.CreatePhotoTourEventLogger(photoTourId);
        var deviceHealth = eventBus.GetDeviceHealthInformation()
            .FirstOrDefault(h => h.Health.DeviceId == photoTourData.DeviceId.ToString());
        if (deviceHealth == default)
        {
            logEvent($"Camera Device {photoTourData.DeviceId} not found. Trying Restart.", PhotoTourEventType.Error);
            RequestRestartDevice(photoTourData.DeviceId.ToString(), photoTourId, photoTourData.DeviceId.ToString()).RunInBackground(ex => ex.LogError());
            return new(null, deviceHealth, hasIrCamera);
        }
        var deviceName = deviceHealth.Health.DeviceName ?? photoTourData.DeviceId.ToString();
        logEvent($"Checking Motor Position {deviceName}", PhotoTourEventType.Information);
        var currentPosition = await deviceApi.MovementClient(deviceHealth.Ip).CurrentpositionAsync();
        if (currentPosition.Dirty == true)
        {
            photoTourData.Finished = true;
            logEvent($"Motor Position is dirty {deviceName}. Phototour is aborted", PhotoTourEventType.Error);
            return new(false, deviceHealth, hasIrCamera);
        }
        logEvent($"Checking Camera {deviceName}", PhotoTourEventType.Information);
        var irTest = hasIrCamera ? await deviceApi.IrImageTakingClient(deviceHealth.Ip).PreviewimageAsync().Try() : default;
        var irImage = irTest.Result?.Stream.ConvertToArray() ?? [];
        var visTest = await deviceApi.VisImageTakingClient(deviceHealth.Ip).PreviewimageAsync().Try();
        var visImage = visTest.Result?.Stream.ConvertToArray() ?? [];
        if ((hasIrCamera && irImage.Length < 100) || (hasIrCamera && !irTest.Error.IsEmpty()) || visImage.Length < 100 || !visTest.Error.IsEmpty())
        {
            var notWorkingCameras = new List<string>()
                .PushIf("IR", _ => irImage.Length < 100)
                .PushIf("VIS", _ => visImage.Length < 100)
                .PushIf($"IR error {irTest.Error}", _ => !irTest.Error.IsEmpty())
                .PushIf($"VIS error {visTest.Error}", _ => !visTest.Error.IsEmpty())
                .Concat(", ");
            logEvent($"Camera {notWorkingCameras} not working. Trying Restart.", PhotoTourEventType.Error);
            RequestRestartDevice(photoTourData.DeviceId.ToString(), photoTourId, deviceName).RunInBackground(ex => ex.LogError());
            return new(null, deviceHealth, hasIrCamera);
        }
        logEvent($"Camera working {deviceName}. IR checked: {hasIrCamera}", PhotoTourEventType.Information);

        if (s_restartRequested.TryGetValue(photoTourData.DeviceId, out var restartsRequested) && restartsRequested > 0)
        {
            logEvent($"Resetting requested restarts of {deviceName} to 0", PhotoTourEventType.Information);
            s_restartRequested.AddOrUpdate(photoTourData.DeviceId, 0, (_1, _2) => 0);
        }
        return new(true, deviceHealth, hasIrCamera);
    }

    public async Task ImmediateRestartDevice(string restartDeviceId, long? photoTourId, string deviceName)
    {
        using var scope = scopeFactory.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var logEvent = photoTourId == null ? Log.Logger.Log : dataContext.CreatePhotoTourEventLogger(photoTourId.Value);
        if (!Guid.TryParse(restartDeviceId, out var deviceGuid))
        {
            logEvent($"Camera Device has no valid GUID: {deviceName}", PhotoTourEventType.Error);
            return;
        }
        s_restartRequested.AddOrUpdate(deviceGuid, FailureThreshold, (_1, _2) => FailureThreshold);
        await RequestRestartDevice(restartDeviceId, photoTourId, deviceName);
    }

    public async Task RequestRestartDevice(string restartDeviceId, long? photoTourId, string deviceName)
    {
        using var scope = scopeFactory.CreateScope();
        var eventBus = scope.ServiceProvider.GetRequiredService<IDeviceConnectionEventBus>();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var deviceApi = scope.ServiceProvider.GetRequiredService<IDeviceApiFactory>();
        var logEvent = photoTourId == null ? Log.Logger.Log : dataContext.CreatePhotoTourEventLogger(photoTourId.Value);
        if (!Guid.TryParse(restartDeviceId, out var deviceGuid))
        {
            logEvent($"Camera Device has no valid GUID: {deviceName}", PhotoTourEventType.Error);
            return;
        }
        s_restartRequested.AddOrUpdate(deviceGuid, 1, (_, r) => r + 1);
        if (s_lastRestarts.TryGetValue(deviceGuid, out var lastRestart) && (DateTime.UtcNow - lastRestart).TotalMinutes < 5)
        {
            logEvent($"Restart is at most possible every 5 minutes: {deviceName}", PhotoTourEventType.Information);
            return;
        }
        if (s_restartRequested.TryGetValue(deviceGuid, out var restartRequests) && restartRequests < FailureThreshold)
        {
            logEvent($"Restart needs atleast {FailureThreshold} consecutive failures. Currently {restartRequests} failures", PhotoTourEventType.Information);
            return;
        }
        var switchData = dataContext.DeviceSwitchAssociations
            .Include(sw => sw.OutletOffFkNavigation)
            .Include(sw => sw.OutletOnFkNavigation)
            .FirstOrDefault(sw => sw.DeviceId == deviceGuid);
        if (switchData == null)
        {
            s_lastRestarts.AddOrUpdate(deviceGuid, DateTime.UtcNow, (_1, _2) => DateTime.UtcNow);
            logEvent($"Automatic switching for {deviceName} not possible. Camera device has no switch assigned!", PhotoTourEventType.Warning);
            return;
        }
        var switchingDevices = eventBus.GetDeviceHealthInformation()
            .Where(h => h.Health.State?.HasFlag(HealthState.CanSwitchOutlets) == true && h.Health.DeviceId != restartDeviceId);
        if (!switchingDevices.Any())
        {
            s_lastRestarts.AddOrUpdate(deviceGuid, DateTime.UtcNow, (_1, _2) => DateTime.UtcNow);
            logEvent("No other devices found capable of switching", PhotoTourEventType.Warning);
            return;
        }
        logEvent($"Safety wait before switching", PhotoTourEventType.Information);
        await Task.Delay(SafetyWaitBeforeRestart);
        foreach (var switchDevice in switchingDevices)
        {
            await deviceApi.SwitchOutletsClient(switchDevice.Ip).SwitchoutletAsync(switchData.OutletOffFkNavigation.Code);
            await Task.Delay(200);
            logEvent($"{switchDevice.Health.DeviceName ?? switchDevice.Health.DeviceId} switched {deviceName} off", PhotoTourEventType.Information);
        }
        eventBus.UpdateDeviceHealths(eventBus.GetDeviceHealthInformation().Where(d => d.Health.DeviceId != restartDeviceId));
        s_lastRestarts.AddOrUpdate(deviceGuid, DateTime.UtcNow, (_1, _2) => DateTime.UtcNow);
        s_restartRequested.AddOrUpdate(deviceGuid, 0, (_1, _2) => 0);
        foreach (var switchDevice in switchingDevices)
        {
            await deviceApi.SwitchOutletsClient(switchDevice.Ip).SwitchoutletAsync(switchData.OutletOnFkNavigation.Code);
            await Task.Delay(200);
            logEvent($"{switchDevice.Health.DeviceName ?? switchDevice.Health.DeviceId} switched {deviceName} on", PhotoTourEventType.Information);
        }
        return;
    }
}
