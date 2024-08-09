using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AutomaticPhotoTour;

namespace Plantmonitor.Server.Features.TemperatureMonitor;

public class DeviceTemperatureWatcherWorker(IServiceScopeFactory scopeFactory) : IHostedService
{
    private Timer? _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(async _ => await RestartMeasurements(), default, 0, (int)TimeSpan.FromSeconds(5).TotalMilliseconds);
        return Task.CompletedTask;
    }

    private async Task RestartMeasurements()
    {
        using var scope = scopeFactory.CreateScope();
        var temperatureWorker = scope.ServiceProvider.GetRequiredService<ITemperatureMeasurementWorker>();
        var restarter = scope.ServiceProvider.GetRequiredService<IDeviceRestarter>();
        await using var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var measurements = temperatureWorker.GetRunningMeasurements().ToLookup(rm => rm.Ip);
        var expectedMeasurements = context.TemperatureMeasurements
            .Where(tm => !tm.Finished)
            .ToDictionary(tm => tm.Id);
        foreach (var measurement in measurements)
        {
            var correctRunningMeasurements = measurement.Select(m => expectedMeasurements.TryGetValue(m.MeasurementId, out var value) ? value : null);
            if (correctRunningMeasurements.Any(rm => rm == null))
            {
                temperatureWorker.StopMeasurement(measurement.Key);
                continue;
            }
            foreach (var id in correctRunningMeasurements.Select(rm => rm!.Id).ToHashSet())
            {
                var lastTemperature = context.TemperatureMeasurementValues
                    .OrderByDescending(tmv => tmv.Timestamp)
                    .FirstOrDefault(tmv => tmv.MeasurementFk == id);
                var data = correctRunningMeasurements.First()!;
                if (lastTemperature != null && (lastTemperature.Timestamp - DateTime.UtcNow).TotalMinutes > 1)
                {
                    await restarter.RestartDevice(data.DeviceId.ToString(), data.PhotoTourFk, data.Comment);
                    break;
                }
                if (lastTemperature == null && (data.StartTime - DateTime.UtcNow).TotalMinutes > 1)
                {
                    await restarter.RestartDevice(data.DeviceId.ToString(), data.PhotoTourFk, data.Comment);
                    break;
                }
            }
        }
        foreach (var measurement in expectedMeasurements.Select(em => em.Value).GroupBy(em => em.DeviceId))
        {
            if (measurement.Any(m => m.IsThermalCamera())) continue;
            var okMeasurements = measurements.SelectMany(m => m.Select(x => x.MeasurementId)).ToHashSet();
            if (measurement.All(m => okMeasurements.Contains(m.Id))) continue;
            await temperatureWorker.ResumeMeasurement(measurement);
        }
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_timer != null) await _timer.DisposeAsync();
    }
}
