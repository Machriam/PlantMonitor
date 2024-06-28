using Plantmonitor.DataModel.DataModel;

namespace Plantmonitor.Server.Features.DeviceProgramming;

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
