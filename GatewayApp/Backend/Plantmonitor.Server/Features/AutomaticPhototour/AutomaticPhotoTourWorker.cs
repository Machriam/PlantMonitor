using Microsoft.AspNetCore.SignalR.Client;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Shared.Features.ImageStreaming;
using Plantmonitor.Server.Features.AppConfiguration;
using Microsoft.EntityFrameworkCore;
using Plantmonitor.Server.Features.DeviceControl;

namespace Plantmonitor.Server.Features.DeviceProgramming;

public class PictureStreamer(IEnvironmentConfiguration configuration)
{
    public async Task StorePhotoTourPictures(string ip, string deviceId, CameraTypeInfo cameraType, StreamingMetaData data, CancellationToken token)
    {
        var picturePath = configuration.PicturePath(deviceId);
        var connection = new HubConnectionBuilder()
            .WithUrl($"https://{ip}/hub/video")
            .AddMessagePackProtocol()
            .Build();
        await connection.StartAsync(token);
        await StreamData(picturePath, cameraType, connection, data, token);
    }

    private static async Task StreamData(string picturePath, CameraTypeInfo cameraInfo, HubConnection connection, StreamingMetaData data, CancellationToken token)
    {
        var sequenceId = DateTime.Now.ToString(CameraStreamFormatter.PictureDateFormat);
        var stream = await connection.StreamAsChannelAsync<byte[]>(cameraInfo.SignalRMethod, data, token);
        var path = Path.Combine(picturePath, sequenceId);
        if (!picturePath.IsEmpty()) Directory.CreateDirectory(path);
        while (await stream.WaitToReadAsync(token))
        {
            await foreach (var image in stream.ReadAllAsync(token))
            {
                var cameraStream = CameraStreamFormatter.FromBytes(image);
                if (!picturePath.IsEmpty() && cameraStream.PictureData != null)
                {
                    cameraStream.WriteToFile(path, cameraInfo);
                }
            }
        }
    }
}

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
        var (healthy, restartSuccessfull) = await CheckDeviceHealth(photoTourId, scope, dataContext);
        if (restartSuccessfull == true && healthy != true)
        {
            (healthy, restartSuccessfull) = await CheckDeviceHealth(photoTourId, scope, dataContext);
            if (healthy != true)
            {
                dataContext.PhotoTourJourneys.Add(new PhotoTourJourney()
                {
                    IrDataFolder = "",
                    VisDataFolder = "",
                    PhotoTourFk = photoTourId,
                    Timestamp = DateTime.UtcNow,
                });
                CreateLogger(dataContext, photoTourId)("Restart was not successfull. Aborting Journey.", PhotoTourEventType.Error);
                dataContext.SaveChanges();
                return;
            }
        }

        dataContext.PhotoTourJourneys.Add(new PhotoTourJourney()
        {
            IrDataFolder = "",
            VisDataFolder = "",
            PhotoTourFk = photoTourId,
            Timestamp = DateTime.UtcNow,
        });
        dataContext.SaveChanges();
    }

    private static async Task<(bool? DeviceHealthy, bool? RestartSuccessfull)> CheckDeviceHealth(long photoTourId, IServiceScope scope, DataContext dataContext)
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
            return (null, success);
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
            return (null, sucess);
        }
        logEvent($"Camera working {photoTourData.DeviceId}", PhotoTourEventType.Information);

        return (true, null);
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
