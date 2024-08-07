using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.DeviceControl;
using Plantmonitor.Shared.Features.ImageStreaming;
using Plantmonitor.Shared.Features.MeasureTemperature;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Plantmonitor.Server.Features.TemperatureMonitor
{
    public class TemperatureStreamingHub(IEnvironmentConfiguration configuration, IDeviceApiFactory factory,
        IDeviceConnectionStorage deviceConnections) : Hub
    {
        private static readonly ConcurrentDictionary<string, string> s_ipByConnectionId = new();

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (s_ipByConnectionId.TryGetValue(Context.ConnectionId, out var data))
            {
                await factory.TemperatureClient(data).StopmeasuringAsync();
            };
            await base.OnDisconnectedAsync(exception);
        }

        public async Task<ChannelReader<TemperatureStreamData>> StreamTemperature(string[] devices, string ip, CancellationToken token)
        {
            var deviceId = deviceConnections.GetCurrentDeviceHealths().First(h => h.Ip == ip).Health.DeviceId;
            s_ipByConnectionId.TryAdd(Context.ConnectionId, ip);
            var channel = Channel.CreateBounded<TemperatureStreamData>(new BoundedChannelOptions(100)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
            });
            var connection = new HubConnectionBuilder()
                .WithUrl($"https://{ip}/hub/temperatures")
                .AddMessagePackProtocol()
                .Build();
            await connection.StartAsync(token);
            StreamData(channel, connection, devices, token).RunInBackground(ex => ex.LogError());
            return channel.Reader;
        }

        public ChannelReader<CameraStreamData> StreamPictureSeries(string deviceId, string sequenceId)
        {
            var channel = Channel.CreateBounded<CameraStreamData>(new BoundedChannelOptions(1)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
            });
            var directory = Path.Combine(configuration.PicturePath(deviceId), sequenceId);
            if (!Path.Exists(directory)) throw new Exception($"Path {directory} could not be found");
            var files = Directory.EnumerateFiles(directory).OrderBy(f => f).ToList();
            StreamFiles(files, channel).RunInBackground(ex => ex.LogError());
            return channel.Reader;
        }

        private async Task StreamFiles(IList<string> fileList, Channel<CameraStreamData> channel)
        {
            foreach (var file in fileList)
            {
                var success = CameraStreamFormatter.FromFile(file, out var streamData);
                if (!success) continue;
                await channel.Writer.WriteAsync(streamData.ConvertToStreamObject());
                await Task.Delay(10);
            }
            await Task.Delay(5000);
            Context.Abort();
        }

        private static async Task StreamData(Channel<TemperatureStreamData> channel, HubConnection connection, string[] devices, CancellationToken token)
        {
            var stream = await connection.StreamAsChannelAsync<TemperatureStreamData>("StreamTemperatureData", devices, token);
            while (await stream.WaitToReadAsync(token))
            {
                await foreach (var image in stream.ReadAllAsync(token))
                {
                    await channel.Writer.WriteAsync(image, token);
                }
            }
        }
    }
}
