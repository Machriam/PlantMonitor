using Emgu.CV.Ocl;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using System.Threading.Channels;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Shared.Features.ImageStreaming;
using Plantmonitor.Server.Features.AppConfiguration;

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
        dataContext.PhotoTourEvents.Add(new PhotoTourEvent()
        {
            Message = "Dry Run",
            Type = PhotoTourEventType.Information,
            PhotoTourFk = photoTourId,
            Timestamp = DateTime.UtcNow,
        });
        dataContext.PhotoTourJourneys.Add(new PhotoTourJourney()
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
