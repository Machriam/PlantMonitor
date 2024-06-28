using Microsoft.AspNetCore.SignalR.Client;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.DeviceControl;
using Plantmonitor.Shared.Features.MeasureTemperature;
using Serilog;

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
            using var scope = scopeFactory.CreateScope();
            var connectedDevices = scope.ServiceProvider.GetRequiredService<IDeviceConnectionEventBus>();
            var healthInfo = connectedDevices.GetDeviceHealthInformation().FirstOrDefault(h => h.Ip == ip);
            var deviceGuid = Guid.Parse(healthInfo.Health.DeviceId ?? throw new Exception($"Device {ip} has no Device Id"));
            var token = new CancellationTokenSource();
            var connection = new HubConnectionBuilder()
                .WithUrl($"https://{ip}/hub/temperatures")
                .WithAutomaticReconnect(new RetryPolicy1Second())
                .AddMessagePackProtocol()
                .Build();
            await connection.StartAsync(token.Token);
            StoreData(connection, ip, devices, deviceGuid, token).RunInBackground(ex =>
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

        private async Task StoreData(HubConnection connection, string ip, MeasurementDevice[] devices, Guid deviceGuid, CancellationTokenSource token)
        {
            using var scope = scopeFactory.CreateScope();
            await using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
            var measurements = devices.Select(d => new TemperatureMeasurement()
            {
                SensorId = d.SensorId,
                Comment = d.Comment,
                DeviceId = deviceGuid,
                StartTime = DateTime.UtcNow,
            }).ToList();
            dataContext.TemperatureMeasurements.AddRange(measurements);
            dataContext.SaveChanges();
            foreach (var measurement in measurements) s_runningMeasurements.Add(new RunningMeasurementInfo(ip, measurement.Id, token));
            var deviceArray = devices.Select(d => d.SensorId).ToArray() ?? [];
            var stream = await connection.StreamAsChannelAsync<TemperatureStreamData>("StreamTemperatureData", deviceArray, token.Token);
            while (await stream.WaitToReadAsync(token.Token))
            {
                await foreach (var measurement in stream.ReadAllAsync(token.Token))
                {
                    Log.Logger.Information("Received temperature data: {data}", measurement);
                    dataContext.TemperatureMeasurementValues.Add(new TemperatureMeasurementValue()
                    {
                        Temperature = measurement.TemperatureInC,
                        Timestamp = measurement.Time,
                        MeasurementFk = measurements.First(m => m.SensorId == measurement.Device).Id,
                    });
                    dataContext.SaveChanges();
                }
            }
        }
    }

    public record struct RunningMeasurementInfo(string Ip, long MeasurementId, CancellationTokenSource Token);
}
