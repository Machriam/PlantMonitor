using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.DeviceConfiguration;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Channels;

namespace Plantmonitor.Server.Features.DeviceControl
{
    public record struct StoredPictureData(DateTime PictureDate, byte[] Picture, int Steps);

    public class PictureStreamingHub(IEnvironmentConfiguration configuration, ILogger<PictureStreamingHub> logger, IDeviceApiFactory factory,
        IDeviceConnectionEventBus deviceConnections) : Hub
    {
        private const string PictureDateFormat = "yyyy-MM-dd HH-mm-ss-fff";
        private static readonly ConcurrentDictionary<string, string> _ipByConnectionId = new();

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_ipByConnectionId.TryGetValue(Context.ConnectionId, out var ip))
            {
                await factory.ImageTakingClient(ip).KillcameraAsync();
            };
            await base.OnDisconnectedAsync(exception);
        }

        public ChannelReader<StoredPictureData> StreamPictureSeries(string deviceId, string sequenceId)
        {
            var channel = Channel.CreateBounded<StoredPictureData>(new BoundedChannelOptions(1)
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

        private async Task StreamFiles(IList<string> fileList, Channel<StoredPictureData> channel)
        {
            foreach (var file in fileList)
            {
                var split = Path.GetFileNameWithoutExtension(file).Split('_');
                if (split.Length < 2) continue;
                if (!DateTime.TryParseExact(split[0], PictureDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;
                if (!int.TryParse(split[1], out var steps)) continue;
                await channel.Writer.WriteAsync(new StoredPictureData(date, File.ReadAllBytes(file), steps));
                await Task.Delay(10);
            }
            await Task.Delay(5000);
            Context.Abort();
        }

        public async Task<ChannelReader<byte[]>> StreamPictures(float resolutionDivider, int quality, float distanceInM, string ip, bool storeData, CancellationToken token)
        {
            var deviceId = deviceConnections.GetDeviceHealthInformation().First(h => h.Ip == ip).Health.DeviceId;
            _ipByConnectionId.TryAdd(Context.ConnectionId, ip);
            var picturePath = storeData ? configuration.PicturePath(deviceId) : "";
            var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
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
            _ = StreamData(resolutionDivider, quality, distanceInM, picturePath, storeData, channel, connection, token);
            return channel.Reader;
        }

        private async Task StreamData(float resolutionDivider, int quality, float distanceInM, string picturePath, bool storeData,
            Channel<byte[]> channel, HubConnection connection, CancellationToken token)
        {
            var sequenceId = DateTime.Now.ToString("yyyy-MM-dd HH-mm-s");
            var stream = await connection.StreamAsChannelAsync<byte[]>("StreamStoredMjpeg", resolutionDivider, quality,
                distanceInM, storeData, token);
            var path = Path.Combine(picturePath, sequenceId);
            if (!picturePath.IsEmpty()) Directory.CreateDirectory(path);
            while (await stream.WaitToReadAsync(token))
            {
                await foreach (var image in stream.ReadAllAsync(token))
                {
                    if (!picturePath.IsEmpty())
                    {
                        var steps = BitConverter.ToInt32(image.AsSpan()[0..4]);
                        var date = new DateTime(BitConverter.ToInt64(image.AsSpan()[4..12]));
                        if (image.Length > 12) File.WriteAllBytes(Path.Combine(path, $"{date.ToUniversalTime().ToString(PictureDateFormat)}_{steps}.jpg"), image[12..]);
                    }
                    var result = await channel.Writer.WriteAsync(image, token).Try();
                    if (!result.IsEmpty()) logger.LogWarning("Could not write Picturestream {error}", result);
                }
            }
        }
    }
}
