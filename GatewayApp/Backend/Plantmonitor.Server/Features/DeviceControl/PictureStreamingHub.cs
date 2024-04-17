using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Plantmonitor.Server.Features.AppConfiguration;
using System.Threading.Channels;

namespace Plantmonitor.Server.Features.DeviceControl
{
    public class PictureStreamingHub(IEnvironmentConfiguration configuration, ILogger<PictureStreamingHub> logger) : Hub
    {
        public async Task<ChannelReader<byte[]>> StreamPictures(float resolutionDivider, int quality, float distanceInM, string ip, CancellationToken token)
        {
            var picturePath = configuration.PicturePath(ip);
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
            _ = StreamData(resolutionDivider, quality, distanceInM, picturePath, channel, connection, token);
            return channel.Reader;
        }

        private async Task StreamData(float resolutionDivider, int quality, float distanceInM, string picturePath,
            Channel<byte[]> channel, HubConnection connection, CancellationToken token)
        {
            var stream = await connection.StreamAsChannelAsync<byte[]>("StreamMjpeg", resolutionDivider, quality, distanceInM, token);
            while (await stream.WaitToReadAsync(token))
            {
                await foreach (var image in stream.ReadAllAsync(token))
                {
                    var result = await channel.Writer.WriteAsync(image, token).Try();
                    if (!result.IsEmpty()) logger.LogWarning("Could not write Picturestream {error}", result);
                }
            }
        }
    }
}