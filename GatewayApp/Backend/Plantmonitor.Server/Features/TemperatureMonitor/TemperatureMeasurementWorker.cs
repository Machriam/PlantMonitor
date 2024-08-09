using Microsoft.AspNetCore.SignalR.Client;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.AutomaticPhotoTour;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Shared.Features.MeasureTemperature;
using Serilog;

namespace Plantmonitor.Server.Features.TemperatureMonitor
{
    public interface ITemperatureMeasurementWorker
    {
        IEnumerable<RunningMeasurementInfo> GetRunningMeasurements();

        void StopMeasurement(string ip);

        Task StartTemperatureMeasurement(MeasurementDevice[] devices, string ip, long? photoTourFk = null);

        Task ResumeMeasurement(IEnumerable<TemperatureMeasurement> measurements);
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

        public Task ResumeMeasurement(IEnumerable<TemperatureMeasurement> measurements)
        {
            var measurementIds = measurements.Select(m => m.Id).ToHashSet();
            ResumeMeasurementInternal(measurements).RunInBackground(ex =>
            {
                ex.LogError();
                s_runningMeasurements.RemoveAll(x => measurementIds.Contains(x.MeasurementId));
            });
            return Task.CompletedTask;
        }

        private async Task ResumeMeasurementInternal(IEnumerable<TemperatureMeasurement> measurements)
        {
            using var scope = scopeFactory.CreateScope();
            await using var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
            var deviceRestarter = scope.ServiceProvider.GetRequiredService<IDeviceRestarter>();
            var connectedDevices = scope.ServiceProvider.GetRequiredService<IDeviceConnectionEventBus>();
            var deviceId = measurements.FirstOrDefault()?.DeviceId.ToString();
            var healthInfo = connectedDevices.GetDeviceHealthInformation()
                .FirstOrDefault(h => h.Health.DeviceId == deviceId);
            if (healthInfo == default)
            {
                if (!deviceId.IsEmpty()) await deviceRestarter.RestartDevice(deviceId!, measurements.FirstOrDefault()?.PhotoTourFk, deviceId ?? "NA");
                return;
            }
            var token = new CancellationTokenSource();
            var connection = new HubConnectionBuilder()
                .WithUrl($"https://{healthInfo.Ip}/hub/temperatures")
                .WithAutomaticReconnect(new SignarRRetryPolicy1Second())
                .AddMessagePackProtocol()
                .Build();
            await connection.StartAsync(token.Token);
            foreach (var measurement in measurements) s_runningMeasurements.Add(new RunningMeasurementInfo(healthInfo.Ip, measurement.Id, token));
            var deviceArray = measurements.Select(d => d.SensorId).ToArray() ?? [];
            await StoreMeasurementValues(measurements, dataContext, token, connection, deviceArray);
        }

        private static async Task StoreMeasurementValues(IEnumerable<TemperatureMeasurement> measurements, IDataContext dataContext, CancellationTokenSource token, HubConnection connection, string[] deviceArray)
        {
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

        public async Task StartTemperatureMeasurement(MeasurementDevice[] devices, string ip, long? photoTourFk = null)
        {
            using var scope = scopeFactory.CreateScope();
            var connectedDevices = scope.ServiceProvider.GetRequiredService<IDeviceConnectionEventBus>();
            var healthInfo = connectedDevices.GetDeviceHealthInformation().FirstOrDefault(h => h.Ip == ip);
            var deviceGuid = Guid.Parse(healthInfo.Health.DeviceId ?? throw new Exception($"Device {ip} has no Device Id"));
            var token = new CancellationTokenSource();
            var connection = new HubConnectionBuilder()
                .WithUrl($"https://{ip}/hub/temperatures")
                .WithAutomaticReconnect(new SignarRRetryPolicy1Second())
                .AddMessagePackProtocol()
                .Build();
            await connection.StartAsync(token.Token);
            StoreData(connection, ip, devices, deviceGuid, photoTourFk, token).RunInBackground(ex =>
                {
                    ex.LogError();
                    s_runningMeasurements.RemoveAll(x => x.Ip == ip);
                });
        }

        public void StopMeasurement(string ip)
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
            foreach (var measurement in s_runningMeasurements.Where(m => m.Ip == ip).ToList())
            {
                measurement.Token.Cancel();
                var dbMeasurement = dataContext.TemperatureMeasurements.FirstOrDefault(tm => tm.Id == measurement.MeasurementId);
                if (dbMeasurement != null) dbMeasurement.Finished = true;
            }
            dataContext.SaveChanges();
            s_runningMeasurements.RemoveAll(m => m.Ip == ip);
        }

        public IEnumerable<RunningMeasurementInfo> GetRunningMeasurements()
        {
            return s_runningMeasurements;
        }

        private async Task StoreData(HubConnection connection, string ip, MeasurementDevice[] devices, Guid deviceGuid, long? photoTourFk, CancellationTokenSource token)
        {
            using var scope = scopeFactory.CreateScope();
            await using var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
            var measurements = CreateMeasurements(devices, deviceGuid, photoTourFk, dataContext);
            foreach (var measurement in measurements) s_runningMeasurements.Add(new RunningMeasurementInfo(ip, measurement.Id, token));
            var deviceArray = devices.Select(d => d.SensorId).ToArray() ?? [];
            await StoreMeasurementValues(measurements, dataContext, token, connection, deviceArray);
        }

        private static List<TemperatureMeasurement> CreateMeasurements(MeasurementDevice[] devices, Guid deviceGuid, long? photoTourFk, IDataContext dataContext)
        {
            var measurements = devices.Select(d => new TemperatureMeasurement()
            {
                SensorId = d.SensorId,
                Comment = d.Comment,
                PhotoTourFk = photoTourFk,
                DeviceId = deviceGuid,
                StartTime = DateTime.UtcNow,
            }).ToList();
            dataContext.TemperatureMeasurements.AddRange(measurements);
            dataContext.SaveChanges();
            return measurements;
        }
    }

    public record struct RunningMeasurementInfo(string Ip, long MeasurementId, CancellationTokenSource Token);
}
