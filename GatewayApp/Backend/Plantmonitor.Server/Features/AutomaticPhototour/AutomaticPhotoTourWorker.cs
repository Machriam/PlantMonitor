using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Microsoft.EntityFrameworkCore;
using Plantmonitor.Server.Features.DeviceControl;
using Plantmonitor.Shared.Features.ImageStreaming;
using Microsoft.OpenApi.Extensions;

namespace Plantmonitor.Server.Features.DeviceProgramming;

public class AutomaticPhotoTourWorker(IServiceScopeFactory serviceProvider) : IHostedService
{
    private delegate void EventLogger(string message, PhotoTourEventType type);

    private static EventLogger CreateLogger(DataContext context, long photourId) => (message, type) => LogEvent(context, message, photourId, type);

    private static void LogEvent(DataContext context, string message, long phototourId, PhotoTourEventType type = PhotoTourEventType.Information)
    {
        context.PhotoTourEvents.Add(new PhotoTourEvent()
        {
            Message = message,
            Type = type,
            PhotoTourFk = phototourId,
            Timestamp = DateTime.UtcNow,
        });
        context.SaveChanges();
    }

    private static Timer? s_scheduleTimer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        s_scheduleTimer = new Timer(async _ => await SchedulePhotoTours(), default, 0, (int)TimeSpan.FromSeconds(5).TotalMilliseconds);
        return Task.CompletedTask;
    }

    private async Task SchedulePhotoTours()
    {
        using var scope = serviceProvider.CreateScope();
        await using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
        foreach (var photoTour in dataContext.AutomaticPhotoTours.Where(pt => !pt.Finished))
        {
            var lastJourney = dataContext.PhotoTourJourneys.OrderByDescending(j => j.Timestamp)
                .FirstOrDefault(j => j.PhotoTourFk == photoTour.Id);
            if (lastJourney == default || (lastJourney.Timestamp - DateTime.UtcNow).TotalMinutes >= photoTour.IntervallInMinutes)
            {
                RunPhotoTour(photoTour.Id).RunInBackground(ex => ex.LogError());
            }
        }
    }

    private async Task RunPhotoTour(long photoTourId)
    {
        await Task.Yield();
        using var scope = serviceProvider.CreateScope();
        await using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
        await using var irStreamer = scope.ServiceProvider.GetRequiredService<IPictureDiskStreamer>();
        await using var visStreamer = scope.ServiceProvider.GetRequiredService<IPictureDiskStreamer>();
        var deviceApi = scope.ServiceProvider.GetRequiredService<IDeviceApiFactory>();
        var (healthy, restartSuccessfull, device) = await CheckDeviceHealth(photoTourId, scope, dataContext);
        var deviceGuid = Guid.Parse(device.Health.DeviceId ?? "");
        if (restartSuccessfull == true && healthy != true)
        {
            (healthy, restartSuccessfull, device) = await CheckDeviceHealth(photoTourId, scope, dataContext);
            if (healthy != true)
            {
                CreateEmptyJourney(photoTourId, dataContext);
                CreateLogger(dataContext, photoTourId)("Restart was not successfull. Aborting Journey.", PhotoTourEventType.Error);
                return;
            }
        }
        var (irFolder, visFolder) = await TakePhotos(photoTourId, dataContext, irStreamer, visStreamer, deviceApi, device, deviceGuid);

        dataContext.PhotoTourJourneys.Add(new PhotoTourJourney()
        {
            IrDataFolder = irFolder,
            VisDataFolder = visFolder,
            PhotoTourFk = photoTourId,
            Timestamp = DateTime.UtcNow,
        });
        dataContext.SaveChanges();
    }

    private static async Task<(string IrFolder, string VisFolder)> TakePhotos(long photoTourId, DataContext dataContext, IPictureDiskStreamer irStreamer, IPictureDiskStreamer visStreamer, IDeviceApiFactory deviceApi, DeviceHealthState device, Guid deviceGuid)
    {
        var irFolder = "";
        var visFolder = "";
        var movementClient = deviceApi.MovementClient(device.Ip);
        var irClient = deviceApi.IrImageTakingClient(device.Ip);
        var currentPosition = await movementClient.CurrentpositionAsync();
        if (currentPosition != 0) await movementClient.MovemotorAsync(-currentPosition, 1000, 4000, 400);
        var movementPlan = dataContext.DeviceMovements.FirstOrDefault(dm => dm.DeviceId == deviceGuid);
        if (movementPlan == null)
        {
            CreateLogger(dataContext, photoTourId)("No Movementplan found. Aborting", PhotoTourEventType.Error);
            CreateEmptyJourney(photoTourId, dataContext);
            return ("", "");
        }
        var pointsToReach = new List<int>();
        foreach (var point in movementPlan.MovementPlan.StepPoints) pointsToReach.Add(pointsToReach.LastOrDefault() + point.StepOffset);
        var currentStep = 0;
        var visPosition = 0;
        var irPosition = 0;
        var deviceTemperature = dataContext.AutomaticPhotoTours
            .Include(pt => pt.TemperatureMeasurements)
            .First(pt => pt.Id == photoTourId)
            .TemperatureMeasurements.First(tm => tm.DeviceId == deviceGuid);
        Task DataReceived(CameraStreamFormatter data, CameraType type)
        {
            if (type == CameraType.IR) irPosition = data.Steps;
            if (type == CameraType.Vis) visPosition = data.Steps;
            if (data.TemperatureInK != default)
            {
                deviceTemperature.TemperatureMeasurementValues
                    .Add(new TemperatureMeasurementValue() { Temperature = data.TemperatureInK.KelvinToCelsius(), Timestamp = DateTime.UtcNow });
                dataContext.SaveChanges();
            }
            return Task.CompletedTask;
        }
        irStreamer.StartStreamingToDisc(device.Ip, device.Health.DeviceId ?? "", CameraType.IR.GetAttributeOfType<CameraTypeInfo>(),
             StreamingMetaData.Create(1, 100, default, true, [.. pointsToReach], CameraType.IR),
             x => irFolder = x, x => DataReceived(x, CameraType.IR), CancellationToken.None).RunInBackground(ex => ex.LogError());
        visStreamer.StartStreamingToDisc(device.Ip, device.Health.DeviceId ?? "", CameraType.IR.GetAttributeOfType<CameraTypeInfo>(),
             StreamingMetaData.Create(1, 100, movementPlan.MovementPlan.StepPoints.FirstOrDefault().FocusInCentimeter, true, [.. pointsToReach], CameraType.Vis),
             x => visFolder = x, x => DataReceived(x, CameraType.Vis), CancellationToken.None).RunInBackground(ex => ex.LogError());
        foreach (var step in movementPlan.MovementPlan.StepPoints)
        {
            await deviceApi.MovementClient(device.Ip).MovemotorAsync(step.StepOffset, 1000, 4000, 400);
            currentStep += step.StepOffset;
            while (currentStep != irPosition || currentStep != visPosition) await Task.Delay(100);
            await irClient.RunffcAsync();
            await Task.Delay(5000);
        }

        await deviceApi.IrImageTakingClient(device.Ip).KillcameraAsync();
        await deviceApi.VisImageTakingClient(device.Ip).KillcameraAsync();
        return (irFolder, visFolder);
    }

    private static void CreateEmptyJourney(long photoTourId, DataContext dataContext)
    {
        dataContext.PhotoTourJourneys.Add(new PhotoTourJourney()
        {
            IrDataFolder = "",
            VisDataFolder = "",
            PhotoTourFk = photoTourId,
            Timestamp = DateTime.UtcNow,
        });
        dataContext.SaveChanges();
    }

    private static async Task<(bool? DeviceHealthy, bool? RestartSuccessfull, DeviceHealthState ImagingDevice)> CheckDeviceHealth(long photoTourId, IServiceScope scope, DataContext dataContext)
    {
        var eventBus = scope.ServiceProvider.GetRequiredService<IDeviceConnectionEventBus>();
        var deviceApi = scope.ServiceProvider.GetRequiredService<IDeviceApiFactory>();
        var photoTourData = dataContext.AutomaticPhotoTours
            .Include(apt => apt.TemperatureMeasurements)
            .First(apt => apt.Id == photoTourId);
        var logEvent = CreateLogger(dataContext, photoTourId);
        var deviceHealth = eventBus.GetDeviceHealthInformation()
            .FirstOrDefault(h => h.Health.DeviceId == photoTourData.DeviceId.ToString());
        if (deviceHealth == default)
        {
            logEvent($"Camera Device {photoTourData.DeviceId} not found. Trying Restart.", PhotoTourEventType.Error);
            var success = await RestartDevice(dataContext, eventBus, deviceApi, photoTourData.DeviceId.ToString(), logEvent);
            return (null, success, deviceHealth);
        }
        logEvent($"Checking Camera {photoTourData.DeviceId}", PhotoTourEventType.Information);
        var irTest = await deviceApi.IrImageTakingClient(deviceHealth.Ip).PreviewimageAsync();
        var visTest = await deviceApi.VisImageTakingClient(deviceHealth.Ip).PreviewimageAsync();
        var irImage = irTest.Stream.ConvertToArray();
        var visImage = visTest.Stream.ConvertToArray();
        if (irImage.Length < 100 || visImage.Length < 100)
        {
            var notWorkingCameras = new[] { irImage.Length < 100 ? "IR" : "", visImage.Length < 100 ? "VIS" : "" }.Concat(", ");
            logEvent($"Camera {notWorkingCameras} not working. Trying Restart.", PhotoTourEventType.Error);
            var sucess = await RestartDevice(dataContext, eventBus, deviceApi, photoTourData.DeviceId.ToString(), logEvent);
            return (null, sucess, deviceHealth);
        }
        logEvent($"Camera working {photoTourData.DeviceId}", PhotoTourEventType.Information);

        return (true, null, deviceHealth);
    }

    private static async Task<bool> RestartDevice(DataContext dataContext, IDeviceConnectionEventBus eventBus, IDeviceApiFactory deviceApi, string restartDeviceId, EventLogger logEvent)
    {
        if (!Guid.TryParse(restartDeviceId, out var deviceGuid))
        {
            logEvent($"Camera Device has no valid GUID: {restartDeviceId}", PhotoTourEventType.Error);
            return false;
        };
        var switchData = dataContext.DeviceSwitchAssociations
            .Include(sw => sw.OutletOffFkNavigation)
            .Include(sw => sw.OutletOnFkNavigation)
            .FirstOrDefault(sw => sw.DeviceId == deviceGuid);
        if (switchData == null)
        {
            logEvent($"Automatic switching for {restartDeviceId} not possible. Camera device has no switch assigned!", PhotoTourEventType.Warning);
            return false;
        }
        var switchingDevices = eventBus.GetDeviceHealthInformation()
            .Where(h => h.Health.State?.HasFlag(HealthState.CanSwitchOutlets) == true && h.Health.DeviceId != restartDeviceId);
        if (!switchingDevices.Any())
        {
            logEvent("No other devices found capable of switching", PhotoTourEventType.Warning);
            return false;
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
        for (var i = 0; i < 120; i++)
        {
            await Task.Delay(1000);
            if (eventBus.GetDeviceHealthInformation().Any(d => d.Health.DeviceId == restartDeviceId)) return true;
        }
        return false;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        s_scheduleTimer?.Dispose();
        return Task.CompletedTask;
    }
}
