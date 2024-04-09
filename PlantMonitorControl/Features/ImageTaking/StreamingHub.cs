using Iot.Device.Camera.Settings;
using Iot.Device.Common;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Threading.Channels;

namespace PlantMonitorControl.Features.MotorMovement;

public class StreamingHub([FromKeyedServices(ICameraInterop.VisCamera)] ICameraInterop cameraInterop) : Hub
{
    public ChannelReader<byte[]> StreamVideo(CancellationToken token)
    {
        var channel = Channel.CreateUnbounded<byte[]>();
        var (ms, _) = cameraInterop.VideoStream();
        _ = WriteItemsAsync(channel, ms, token);
        return channel.Reader;
    }

    private static async Task WriteItemsAsync(ChannelWriter<byte[]> writer, MemoryStream ms, CancellationToken token)
    {
        var buffer = new byte[1024];
        while (true)
        {
            if (ms.Length <= ms.Position) ms.Position = 0;
            var length = await ms.ReadAsync(buffer, token);
            for (var i = 0; i < length; i++)
            {
                await writer.WriteAsync(buffer, token);
            }
            await Task.Yield();
        }
    }
}