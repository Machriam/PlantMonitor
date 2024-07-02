using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Microsoft.EntityFrameworkCore;
using Plantmonitor.Server.Features.DeviceControl;
using Plantmonitor.Shared.Features.ImageStreaming;
using Microsoft.OpenApi.Extensions;

namespace Plantmonitor.Server.Features.AutomaticPhotoTour;

public class AutomaticPhotoTourWorker(IServiceScopeFactory serviceProvider) : IHostedService
{
    private static bool _photoTripRunning;
    private static object _lock = new();

    private static Timer? s_scheduleTimer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        s_scheduleTimer = new Timer(async _ => await SchedulePhotoTrips(), default, 0, (int)TimeSpan.FromSeconds(5).TotalMilliseconds);
        return Task.CompletedTask;
    }

    private async Task SchedulePhotoTrips()
    {
        using var scope = serviceProvider.CreateScope();
        await using var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        foreach (var photoTour in dataContext.AutomaticPhotoTours.Where(pt => !pt.Finished))
        {
            var lastTrip = dataContext.PhotoTourTrips.OrderByDescending(j => j.Timestamp)
                .FirstOrDefault(j => j.PhotoTourFk == photoTour.Id);
            if (lastTrip == default || (lastTrip.Timestamp - DateTime.UtcNow).TotalMinutes >= photoTour.IntervallInMinutes)
            {
                RunPhotoTrip(photoTour.Id).RunInBackground(ex => ex.LogError());
            }
        }
    }

    private async Task RunPhotoTrip(long photoTourId)
    {
        await Task.Yield();
        lock (_lock)
        {
            if (_photoTripRunning) return;
            _photoTripRunning = true;
        }
        using var scope = serviceProvider.CreateScope();
        await using var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        await using var irStreamer = scope.ServiceProvider.GetRequiredService<IPictureDiskStreamer>();
        await using var visStreamer = scope.ServiceProvider.GetRequiredService<IPictureDiskStreamer>();
        var deviceApi = scope.ServiceProvider.GetRequiredService<IDeviceApiFactory>();
        var (healthy, device) = await CheckDeviceHealth(photoTourId, scope, dataContext);
        var deviceGuid = Guid.Parse(device.Health.DeviceId ?? "");
        if (healthy != true)
        {
            CreateEmptyTrip(photoTourId, dataContext);
            lock (_lock) _photoTripRunning = false;
            return;
        }
        var (irFolder, visFolder) = await TakePhotos(photoTourId, dataContext, irStreamer, visStreamer, deviceApi, device, deviceGuid);

        dataContext.PhotoTourTrips.Add(new PhotoTourTrip()
        {
            IrDataFolder = irFolder,
            VisDataFolder = visFolder,
            PhotoTourFk = photoTourId,
            Timestamp = DateTime.UtcNow,
        });
        dataContext.SaveChanges();
        lock (_lock) _photoTripRunning = false;
    }

    private static async Task<(string IrFolder, string VisFolder)> TakePhotos(long photoTourId, IDataContext dataContext, IPictureDiskStreamer irStreamer, IPictureDiskStreamer visStreamer, IDeviceApiFactory deviceApi, DeviceHealthState device, Guid deviceGuid)
    {
        var irFolder = "";
        var visFolder = "";
        var movementClient = deviceApi.MovementClient(device.Ip);
        var irClient = deviceApi.IrImageTakingClient(device.Ip);
        var logger = dataContext.CreatePhotoTourEventLogger(photoTourId);
        var currentPosition = await movementClient.CurrentpositionAsync();
        if (currentPosition != 0) await movementClient.MovemotorAsync(-currentPosition, 1000, 4000, 400);
        var movementPlan = dataContext.DeviceMovements.FirstOrDefault(dm => dm.DeviceId == deviceGuid);
        if (movementPlan == null)
        {
            logger("No Movementplan found. Aborting", PhotoTourEventType.Error);
            CreateEmptyTrip(photoTourId, dataContext);
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
        while (!irStreamer.StreamingFinished() || !visStreamer.StreamingFinished()) await Task.Delay(1000);
        return (irFolder, visFolder);
    }

    private static void CreateEmptyTrip(long photoTourId, IDataContext dataContext)
    {
        dataContext.PhotoTourTrips.Add(new PhotoTourTrip()
        {
            IrDataFolder = "",
            VisDataFolder = "",
            PhotoTourFk = photoTourId,
            Timestamp = DateTime.UtcNow,
        });
        dataContext.SaveChanges();
    }

    private static async Task<(bool? DeviceHealthy, DeviceHealthState ImagingDevice)> CheckDeviceHealth(long photoTourId, IServiceScope scope, IDataContext dataContext)
    {
        var eventBus = scope.ServiceProvider.GetRequiredService<IDeviceConnectionEventBus>();
        var deviceApi = scope.ServiceProvider.GetRequiredService<IDeviceApiFactory>();
        var deviceRestarter = scope.ServiceProvider.GetRequiredService<IDeviceRestarter>();
        var photoTourData = dataContext.AutomaticPhotoTours
            .Include(apt => apt.TemperatureMeasurements)
            .First(apt => apt.Id == photoTourId);
        var logEvent = dataContext.CreatePhotoTourEventLogger(photoTourId);
        var deviceHealth = eventBus.GetDeviceHealthInformation()
            .FirstOrDefault(h => h.Health.DeviceId == photoTourData.DeviceId.ToString());
        if (deviceHealth == default)
        {
            logEvent($"Camera Device {photoTourData.DeviceId} not found. Trying Restart.", PhotoTourEventType.Error);
            deviceRestarter.RestartDevice(photoTourData.DeviceId.ToString(), photoTourId).RunInBackground(ex => ex.LogError());
            return (null, deviceHealth);
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
            deviceRestarter.RestartDevice(photoTourData.DeviceId.ToString(), photoTourId).RunInBackground(ex => ex.LogError());
            return (null, deviceHealth);
        }
        logEvent($"Camera working {photoTourData.DeviceId}", PhotoTourEventType.Information);

        return (true, deviceHealth);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        s_scheduleTimer?.Dispose();
        return Task.CompletedTask;
    }
}
