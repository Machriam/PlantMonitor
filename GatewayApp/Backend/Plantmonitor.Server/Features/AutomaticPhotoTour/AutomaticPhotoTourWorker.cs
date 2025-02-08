using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Microsoft.EntityFrameworkCore;
using Plantmonitor.Server.Features.DeviceControl;
using Plantmonitor.Shared.Features.ImageStreaming;
using Microsoft.OpenApi.Extensions;

namespace Plantmonitor.Server.Features.AutomaticPhotoTour;

public class AutomaticPhotoTourWorker(IServiceScopeFactory scopeFactory) : IHostedService
{
    private static bool s_photoTripRunning;
    private readonly int _ffcTimeout = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;
    private readonly int _positionCheckTimeout = (int)TimeSpan.FromMilliseconds(100).TotalMilliseconds;
    private static readonly object s_lock = new();

    private static Timer? s_scheduleTimer;
    private static readonly int s_scheduleTimeOut = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        s_scheduleTimer = new Timer(_ => SchedulePhotoTrips().RunInBackground(ex => ex.LogError()), default, 0, s_scheduleTimeOut);
        using var scope = scopeFactory.CreateScope();
        await using var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        foreach (var tour in dataContext.AutomaticPhotoTours.Where(apt => !apt.Finished))
        {
            dataContext.PhotoTourEvents.Add(new PhotoTourEvent()
            {
                Message = "Phototour was stopped after restart of the Gateway machine",
                PhotoTourFk = tour.Id,
                Timestamp = DateTime.UtcNow,
                Type = PhotoTourEventType.Critical,
            });
            tour.Finished = true;
        }
        foreach (var measurement in dataContext.TemperatureMeasurements.Where(measurement => !measurement.Finished)) measurement.Finished = true;
        dataContext.SaveChanges();
        await Task.CompletedTask;
    }

    private async Task SchedulePhotoTrips()
    {
        using var scope = scopeFactory.CreateScope();
        await using var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        foreach (var photoTour in dataContext.AutomaticPhotoTours.Where(pt => !pt.Finished))
        {
            var lastTrip = dataContext.PhotoTourTrips.OrderByDescending(j => j.Timestamp)
                .FirstOrDefault(j => j.PhotoTourFk == photoTour.Id);
            if (lastTrip == default || (DateTime.UtcNow - lastTrip.Timestamp).TotalMinutes >= photoTour.IntervallInMinutes)
            {
                RunPhotoTrip(photoTour.Id).RunInBackground(ex =>
                {
                    ex.LogError();
                    lock (s_lock) s_photoTripRunning = false;
                });
            }
        }
    }

    private async Task RunPhotoTrip(long photoTourId)
    {
        await Task.Yield();
        lock (s_lock)
        {
            if (s_photoTripRunning) return;
            s_photoTripRunning = true;
        }
        using var scope = scopeFactory.CreateScope();
        await using var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        await using var irStreamer = scope.ServiceProvider.GetRequiredService<IPictureDiskStreamer>();
        await using var visStreamer = scope.ServiceProvider.GetRequiredService<IPictureDiskStreamer>();
        var deviceRestarter = scope.ServiceProvider.GetRequiredService<IDeviceRestarter>();
        var deviceApi = scope.ServiceProvider.GetRequiredService<IDeviceApiFactory>();
        var (healthy, device, hasIr) = await deviceRestarter.CheckDeviceHealth(photoTourId, scope, dataContext);
        if (healthy != true)
        {
            CreateEmptyTrip(photoTourId, dataContext);
            lock (s_lock) s_photoTripRunning = false;
            return;
        }
        var (irFolder, visFolder) = await TakePhotos(photoTourId, dataContext, irStreamer, visStreamer, deviceApi, device);
        if (irFolder.IsEmpty() && visFolder.IsEmpty())
        {
            lock (s_lock) s_photoTripRunning = false;
            return;
        }

        var newTrip = new PhotoTourTrip()
        {
            IrDataFolder = irFolder,
            VisDataFolder = visFolder,
            PhotoTourFk = photoTourId,
            Timestamp = DateTime.UtcNow,
        };
        dataContext.PhotoTourTrips.Add(newTrip);
        dataContext.SaveChanges();
        var logger = dataContext.CreatePhotoTourEventLogger(photoTourId);
        if (TripHasNoPictures(hasIr, irFolder, visFolder, logger))
        {
            var previousTrip = dataContext.PhotoTourTrips
                .Where(ptt => ptt.PhotoTourFk == photoTourId && ptt.Id != newTrip.Id)
                .OrderByDescending(ptt => ptt.Timestamp)
                .FirstOrDefault();
            if (previousTrip == null)
            {
                logger("Previous trip not found", PhotoTourEventType.Warning);
                lock (s_lock) s_photoTripRunning = false;
                return;
            }
            logger($"Checking previous trip: {previousTrip.Timestamp}", PhotoTourEventType.Information);
            if (TripHasNoPictures(hasIr, previousTrip.IrDataFolder, previousTrip.VisDataFolder, logger))
            {
                logger("Previous trip was also unsuccessful. Restarting immediately.", PhotoTourEventType.Error);
                await deviceRestarter.ImmediateRestartDevice(device.Health?.DeviceId ?? "", photoTourId, device.Health?.DeviceName!);
            }
        }
        lock (s_lock) s_photoTripRunning = false;
    }

    private static bool TripHasNoPictures(bool hasIr, string irFolder, string visFolder, DataContext.EventLogger logger)
    {
        if (!Path.Exists(irFolder) || !Path.Exists(visFolder))
        {
            logger($"Folders {irFolder} or {visFolder} not found. Assuming errors were already handled.", PhotoTourEventType.Warning);
            return true;
        }
        var irImageCount = Directory.EnumerateFiles(irFolder).Count();
        var visImageCount = Directory.EnumerateFiles(visFolder).Count();
        var hasNoPictures = (irImageCount == 0 && hasIr) || visImageCount == 0;
        if (hasNoPictures) logger($"Phototour has not delivered photos for a camera: IR-count {irImageCount} VIS-count {visImageCount}", PhotoTourEventType.Warning);
        else logger($"Image counts are correct. IR-count {irImageCount} VIS-count {visImageCount}", PhotoTourEventType.Information);
        return hasNoPictures;
    }

    private async Task<(string IrFolder, string VisFolder)> TakePhotos(long photoTourId, IDataContext dataContext, IPictureDiskStreamer irStreamer, IPictureDiskStreamer visStreamer,
        IDeviceApiFactory deviceApi, DeviceHealthState device)
    {
        var logger = dataContext.CreatePhotoTourEventLogger(photoTourId);
        if (device.Health?.DeviceId == null || !Guid.TryParse(device.Health.DeviceId, out var deviceGuid))
        {
            return AbortPhotoTaking("Device Id not a valid Guid");
        }
        var irFolder = "";
        var visFolder = "";
        var movementClient = deviceApi.MovementClient(device.Ip);
        var irClient = deviceApi.IrImageTakingClient(device.Ip);
        var visClient = deviceApi.VisImageTakingClient(device.Ip);
        var movementPlan = dataContext.DeviceMovements.FirstOrDefault(dm => dm.DeviceId == deviceGuid);
        if (movementPlan == null)
        {
            return AbortPhotoTaking("No Movementplan found. Aborting");
        }
        var (maxStop, minStop) = movementPlan.GetSafetyStops();
        var currentPosition = await movementClient.CurrentpositionAsync();
        if (currentPosition.Engaged != true) logger("Aborting movement, motor is disengaged", PhotoTourEventType.Critical);
        if (currentPosition.Position != 0)
        {
            logger($"Trying to zero position with Offset: {-currentPosition.Position}", PhotoTourEventType.Information);
            await movementClient.MovemotorAsync(-currentPosition.Position, 1000, 4000, 400, maxStop, minStop);
        }
        var pointsToReach = new List<int>();
        foreach (var point in movementPlan.MovementPlan.StepPoints) pointsToReach.Add(pointsToReach.LastOrDefault() + point.StepOffset);
        var currentStep = 0;
        var visPosition = 0;
        var irPosition = 0;
        var deviceTemperature = dataContext.AutomaticPhotoTours
            .Include(pt => pt.TemperatureMeasurements)
            .FirstOrDefault(pt => pt.Id == photoTourId)?
            .TemperatureMeasurements.FirstOrDefault(tm => tm.DeviceId == deviceGuid);
        var firstImageReceived = new HashSet<CameraType>();
        Task DataReceived(CameraStreamFormatter data, CameraType type)
        {
            if (data.PictureData?.Length > 0 && !firstImageReceived.Contains(type))
            {
                firstImageReceived.Add(type);
                logger($"Received first image of type {Enum.GetName(type)}", PhotoTourEventType.Debug);
            }
            if (type == CameraType.IR) irPosition = data.Steps;
            if (type == CameraType.Vis) visPosition = data.Steps;
            if (data.TemperatureInK != default && deviceTemperature != null)
            {
                deviceTemperature.TemperatureMeasurementValues
                    .Add(new TemperatureMeasurementValue() { Temperature = data.TemperatureInK.KelvinToCelsius(), Timestamp = DateTime.UtcNow });
                dataContext.SaveChanges();
            }
            return Task.CompletedTask;
        }
        var (visHealthy, irHealthy) = (true, true);
        irStreamer.StartStreamingToDisc(device.Ip, device.Health.DeviceId ?? "", CameraType.IR.GetAttributeOfType<CameraTypeInfo>(),
             StreamingMetaData.Create(1, 100, default, true, [.. pointsToReach], CameraType.IR),
             x => irFolder = x, x => DataReceived(x, CameraType.IR), CancellationToken.None)
            .RunInBackground(ex =>
             {
                 irHealthy = false;
                 ex.LogError();
                 using var scope = scopeFactory.CreateScope();
                 using var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
                 dataContext.CreatePhotoTourEventLogger(photoTourId)("Ir streaming error: " + ex.Message, PhotoTourEventType.Warning);
             });
        await Task.Delay(10);
        visStreamer.StartStreamingToDisc(device.Ip, device.Health.DeviceId ?? "", CameraType.Vis.GetAttributeOfType<CameraTypeInfo>(),
             StreamingMetaData.Create(1, 100, movementPlan.MovementPlan.StepPoints.FirstOrDefault().FocusInCentimeter, true, [.. pointsToReach], CameraType.Vis),
             x => visFolder = x, x => DataReceived(x, CameraType.Vis), CancellationToken.None)
            .RunInBackground(ex =>
             {
                 visHealthy = false;
                 ex.LogError();
                 using var scope = scopeFactory.CreateScope();
                 using var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
                 dataContext.CreatePhotoTourEventLogger(photoTourId)("Vis streaming error: " + ex.Message, PhotoTourEventType.Warning);
             });
        await Task.Delay(_ffcTimeout);
        var irImageCount = await irClient.CountoftakenimagesAsync();
        var visImageCount = await visClient.CountoftakenimagesAsync();
        logger($"Collected Vis-images: {visImageCount}, collected Ir-images {irImageCount}", PhotoTourEventType.Information);
        foreach (var step in movementPlan.MovementPlan.StepPoints)
        {
            logger($"Moving to position: {currentStep + step.StepOffset}", PhotoTourEventType.Debug);
            await deviceApi.MovementClient(device.Ip).MovemotorAsync(step.StepOffset, 1000, 4000, 400, maxStop, minStop);
            currentStep += step.StepOffset;
            while (currentStep != irPosition || currentStep != visPosition)
            {
                if (!visHealthy || !irHealthy) return AbortPhotoTaking("Streaming error during motor movement has occured. Aborting trip.");
                await Task.Delay(_positionCheckTimeout);
            }
            logger($"Moved to position: {currentStep}, performing FFC", PhotoTourEventType.Information);
            await irClient.RunffcAsync();
            await Task.Delay(_ffcTimeout);
        }

        logger("Killing image taking processes", PhotoTourEventType.Debug);
        await deviceApi.IrImageTakingClient(device.Ip).KillcameraAsync();
        await deviceApi.VisImageTakingClient(device.Ip).KillcameraAsync();
        currentPosition = await movementClient.CurrentpositionAsync();
        await movementClient.MovemotorAsync(-currentPosition.Position, 1000, 4000, 400, maxStop, minStop);
        while (!irStreamer.StreamingFinished() || !visStreamer.StreamingFinished()) await Task.Delay(_positionCheckTimeout);
        logger("Streaming of data has finished", PhotoTourEventType.Information);
        return (irFolder, visFolder);

        (string IrFolder, string VisFolder) AbortPhotoTaking(string reason)
        {
            logger(reason, PhotoTourEventType.Error);
            CreateEmptyTrip(photoTourId, dataContext);
            return ("", "");
        }
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

    public Task StopAsync(CancellationToken cancellationToken)
    {
        s_scheduleTimer?.Dispose();
        return Task.CompletedTask;
    }
}
