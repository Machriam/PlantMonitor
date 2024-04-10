using Iot.Device.Camera.Settings;
using Iot.Device.Common;
using Microsoft.AspNetCore.SignalR;
using Plantmonitor.Shared.Extensions;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading.Channels;

namespace PlantMonitorControl.Features.MotorMovement;

public class StreamingHub([FromKeyedServices(ICameraInterop.VisCamera)] ICameraInterop cameraInterop, ILogger<StreamingHub> logger) : Hub
{
    private static readonly byte[] _headerBytes = [255, 216, 255, 224, 0, 16, 74, 70, 73, 70, 0];
    private const float baseFps = 0.2f;
    private const int maxWidth = 4600;
    private const int maxHeight = 2560;

    public async Task<ChannelReader<byte[]>> StreamVideo(CancellationToken token, int resolutionDivider, int quality)
    {
        var fps = baseFps * resolutionDivider * resolutionDivider;
        var timeBetweenImages = 1f / fps * 1000f;
        var channel = Channel.CreateUnbounded<byte[]>();
        var (pipe, _) = await cameraInterop.VideoStream((int)(maxWidth / resolutionDivider), (int)(maxHeight / resolutionDivider), quality);
        _ = WriteItemsAsync(channel, pipe.Reader, token, (int)timeBetweenImages);
        return channel.Reader;
    }

    private static Func<byte, bool> BuildHeaderFinder()
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

    private async Task WriteItemsAsync(ChannelWriter<byte[]> writer, PipeReader reader, CancellationToken token, int timeBetweenImages)
    {
        try
        {
            var sw = new Stopwatch();
            var imageBuffer = new byte[1024 * 1024 * 8];
            var imageIndex = 0;
            var headerFinder = BuildHeaderFinder();
            var imageStarted = false;
            sw.Start();
            while (true)
            {
                var result = await reader.ReadAsync(token);
                foreach (var buff in result.Buffer)
                {
                    if (sw.ElapsedMilliseconds < timeBetweenImages) break;
                    for (var i = 0; i < buff.Length; i++)
                    {
                        imageBuffer[imageIndex++] = buff.Span[i];
                        if (!headerFinder(buff.Span[i])) continue;
                        var sendBuffer = imageBuffer[0..(imageIndex - _headerBytes.Length)];
                        imageIndex = 0;
                        if (imageStarted)
                        {
                            await writer.WriteAsync(sendBuffer, token);
                            for (var j = 0; j < _headerBytes.Length; j++) imageBuffer[imageIndex++] = _headerBytes[j];
                            sw.Restart();
                            imageStarted = false;
                            break;
                        }
                        else
                        {
                            for (var j = 0; j < _headerBytes.Length; j++) imageBuffer[imageIndex++] = _headerBytes[j];
                            imageStarted = true;
                        }
                    }
                }
                reader.AdvanceTo(result.Buffer.End);
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Video streaming has been cancelled. Error: {Error}\n{Stacktrace}", ex.Message, ex.StackTrace);
        }
    }
}