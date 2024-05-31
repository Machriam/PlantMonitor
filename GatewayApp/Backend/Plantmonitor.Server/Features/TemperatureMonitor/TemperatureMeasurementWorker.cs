using Microsoft.AspNetCore.SignalR.Client;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Shared.Features.MeasureTemperature;

namespace Plantmonitor.Server.Features.TemperatureMonitor
{
    public interface ITemperatureMeasurementWorker
    {
        IEnumerable<RunningMeasurementInfo> GetRunningMeasurements();

        Task StartTemperatureMeasurement(MeasurementDevice[] devices, string ip);

        void StopMeasurement(string ip);
    }

    public class TemperatureMeasurementWorker(IServiceScopeFactory scopeFactory) : IHostedService, ITemperatureMeasurementWorker
    {
        private static readonly List<RunningMeasurementInfo> s_runningMeasurements = [];

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task StartTemperatureMeasurement(MeasurementDevice[] devices, string ip)
        {
            var token = new CancellationTokenSource();
            var connection = new HubConnectionBuilder()
                .WithUrl($"https://{ip}/hub/temperatures")
                .WithAutomaticReconnect(new RetryPolicy1Second())
                .AddMessagePackProtocol()
                .Build();
            await connection.StartAsync(token.Token);
            StoreData(connection, ip, devices, token).RunInBackground(ex =>
                {
                    ex.LogError();
                    StopMeasurement(ip);
                });
        }

        public void StopMeasurement(string ip)
        {
            foreach (var measurement in s_runningMeasurements.Where(m => m.Ip == ip).ToList()) measurement.Token.Cancel();
            s_runningMeasurements.RemoveAll(m => m.Ip == ip);
        }

        public IEnumerable<RunningMeasurementInfo> GetRunningMeasurements()
        {
            return s_runningMeasurements;
        }

        private async Task StoreData(HubConnection connection, string ip, MeasurementDevice[] devices, CancellationTokenSource token)
        {
            using var scope = scopeFactory.CreateScope();
            await using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
            var measurements = devices.Select(d => new TemperatureMeasurement()
            {
                DeviceId = d.DeviceId,
                Comment = d.Comment,
                StartTime = DateTime.UtcNow,
            }).ToList();
            dataContext.TemperatureMeasurements.AddRange(measurements);
            dataContext.SaveChanges();
            foreach (var measurement in measurements) s_runningMeasurements.Add(new RunningMeasurementInfo(ip, measurement.Id, token));
            var deviceArray = devices.Select(d => d.DeviceId).ToArray() ?? [];
            var stream = await connection.StreamAsChannelAsync<TemperatureStreamData>("StreamTemperatureData", deviceArray, token.Token);
            while (await stream.WaitToReadAsync(token.Token))
            {
                await foreach (var measurement in stream.ReadAllAsync(token.Token))
                {
                    dataContext.TemperatureMeasurementValues.Add(new TemperatureMeasurementValue()
                    {
                        Temperature = measurement.TemperatureInC,
                        Timestamp = measurement.Time,
                        MeasurementFk = measurements.First(m => m.DeviceId == measurement.Device).Id,
                    });
                    dataContext.SaveChanges();
                }
            }
        }
    }

    public record struct RunningMeasurementInfo(string Ip, long MeasurementId, CancellationTokenSource Token);
}
