using Iot.Device.Camera.Settings;
using Iot.Device.Common;
using Microsoft.AspNetCore.SignalR;
using Plantmonitor.Shared.Extensions;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading.Channels;

namespace PlantMonitorControl.Features.MotorMovement;

public class StreamingHub([FromKeyedServices(ICameraInterop.VisCamera)] ICameraInterop cameraInterop) : Hub
{
    private static readonly byte[] _headerBytes = new byte[] { 255, 216, 255, 224, 0, 16, 74, 70, 73, 70, 0 };

    public ChannelReader<byte[]> StreamVideo(CancellationToken token)
    {
        var channel = Channel.CreateUnbounded<byte[]>();
        var findHeader = BuildHeaderFinder();
        var (pipe, _) = cameraInterop.VideoStreamPipe();
        _ = WriteItemsAsync(channel, pipe.Reader, token);
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

    private async Task WriteItemsAsync(ChannelWriter<byte[]> writer, PipeReader reader, CancellationToken token)
    {
        try
        {
            var imageBuffer = new byte[1024 * 1024];
            var imageIndex = 0;
            var buffer = new byte[1024];
            var headerFinder = BuildHeaderFinder();
            while (true)
            {
                var result = await reader.ReadAsync();
                foreach (var buff in result.Buffer)
                {
                    for (var i = 0; i < buff.Length; i++)
                    {
                        imageBuffer[imageIndex++] = buff.Span[i];
                        if (!headerFinder(buff.Span[i])) continue;
                        var sendBuffer = imageBuffer[0..(imageIndex - _headerBytes.Length)];
                        imageIndex = 0;
                        await writer.WriteAsync(sendBuffer, token);
                        imageBuffer = new byte[1024 * 1024];
                        for (var j = 0; j < _headerBytes.Length; j++) imageBuffer[imageIndex++] = _headerBytes[j];
                        await Task.Delay(10);
                    }
                }
                reader.AdvanceTo(result.Buffer.Start, result.Buffer.End);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}