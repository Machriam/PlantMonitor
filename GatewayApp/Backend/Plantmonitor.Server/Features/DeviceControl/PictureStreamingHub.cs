using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Shared.Features.ImageStreaming;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Channels;

namespace Plantmonitor.Server.Features.DeviceControl
{
    public class PictureStreamingHub(IEnvironmentConfiguration configuration, ILogger<PictureStreamingHub> logger, IDeviceApiFactory factory,
        IDeviceConnectionEventBus deviceConnections) : Hub
    {
        private static readonly ConcurrentDictionary<string, string> s_ipByConnectionId = new();

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (s_ipByConnectionId.TryGetValue(Context.ConnectionId, out var ip))
            {
                await factory.VisImageTakingClient(ip).KillcameraAsync();
            };
            await base.OnDisconnectedAsync(exception);
        }

        public async Task<ChannelReader<CameraStreamData>> StreamPictures(StreamingMetaData data, string ip, CancellationToken token)
        {
            var deviceId = deviceConnections.GetDeviceHealthInformation().First(h => h.Ip == ip).Health.DeviceId;
            s_ipByConnectionId.TryAdd(Context.ConnectionId, ip);
            var picturePath = data.StoreData ? configuration.PicturePath(deviceId) : "";
            var channel = Channel.CreateBounded<CameraStreamData>(new BoundedChannelOptions(1)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = true,
            });
            var connection = new HubConnectionBuilder()
                .WithUrl($"https://{ip}/hub/video")
                .AddMessagePackProtocol()
                .Build();
            await connection.StartAsync(token);
            _ = StreamData(data, picturePath, channel, connection, token);
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
            _ = StreamFiles(files, channel);
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

        private async Task StreamData(StreamingMetaData data, string picturePath, Channel<CameraStreamData> channel, HubConnection connection, CancellationToken token)
        {
            var cameraInfo = data.GetCameraType().Attribute<CameraTypeInfo>();
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
                    var result = await channel.Writer.WriteAsync(cameraStream.ConvertToStreamObject(), token).Try();
                    if (!result.IsEmpty()) logger.LogWarning("Could not write Picturestream {error}", result);
                }
            }
        }
    }
}
