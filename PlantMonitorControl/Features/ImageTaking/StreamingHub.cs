using Iot.Device.Camera.Settings;
using Iot.Device.Common;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Threading.Channels;

namespace PlantMonitorControl.Features.MotorMovement;

public class StreamingHub([FromKeyedServices(ICameraInterop.VisCamera)] ICameraInterop cameraInterop) : Hub
{
    private static readonly byte[] _headerBytes = new byte[] { 255, 216, 255, 224, 0, 16, 74, 70, 73, 70, 0 };

    public ChannelReader<byte[]> StreamVideo(CancellationToken token)
    {
        var channel = Channel.CreateUnbounded<byte[]>();
        var findHeader = BuildHeaderFinder();
        var (ms, _) = cameraInterop.VideoStream();
        _ = WriteItemsAsync(channel, ms, token);
        return channel.Reader;
    }

    private Func<byte, bool> BuildHeaderFinder()
    {
        var headerIndex = 0;
        return currentByte =>
        {
            if (currentByte == _headerBytes[headerIndex]) headerIndex++;
            else headerIndex = 0;
            var headerFound = headerIndex == _headerBytes.Length;
            if (headerFound) headerIndex = 0;
            return headerFound;
        };
    }

    private async Task WriteItemsAsync(ChannelWriter<byte[]> writer, MemoryStream ms, CancellationToken token)
    {
        var imageBuffer = new byte[1024 * 1024 * 1024];
        var imageIndex = 0;
        var buffer = new byte[1024];
        var headerFinder = BuildHeaderFinder();
        while (true)
        {
            if (ms.Length <= ms.Position) ms.Position = 0;
            var length = await ms.ReadAsync(buffer, token);
            for (var i = 0; i < length; i++)
            {
                imageBuffer[imageIndex++] = buffer[i];
                if (!headerFinder(buffer[i])) continue;
                var sendBuffer = imageBuffer[0..(imageIndex - _headerBytes.Length)];
                imageIndex = 0;
                await writer.WriteAsync(sendBuffer, token);
                imageBuffer = new byte[1024 * 1024 * 1024];
                for (var j = 0; j < _headerBytes.Length; j++) imageBuffer[imageIndex++] = _headerBytes[j];
            }
            await Task.Yield();
        }
    }
}