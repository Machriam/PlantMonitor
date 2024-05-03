using Microsoft.AspNetCore.SignalR;
using System.IO.Pipelines;
using System.Threading.Channels;

namespace PlantMonitorControl.Features.MotorMovement;

public class StreamingHub([FromKeyedServices(ICameraInterop.VisCamera)] ICameraInterop cameraInterop, ILogger<StreamingHub> logger, IMotorPositionCalculator motorPosition) : Hub
{
    private static readonly byte[] s_headerBytes = [255, 216, 255, 224, 0, 16, 74, 70, 73, 70, 0];
    private static readonly string s_tempImagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tempImages");
    private const string CounterFormat = "000000";

    public async Task<ChannelReader<byte[]>> StreamStoredMjpeg(float resolutionDivider, int quality, float distanceInM,
        string sessionId, bool streamAfterCameraKill, CancellationToken token)
    {
        if (Path.Exists(s_tempImagePath)) Directory.Delete(s_tempImagePath, true);
        var storagePath = Path.Combine(s_tempImagePath, sessionId);
        Directory.CreateDirectory(storagePath);
        var counter = 0;
        var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = true,
        });
        var (pipe, _) = await cameraInterop.MjpegStream(resolutionDivider, quality, distanceInM);
        _ = WriteItemsAsync(async (b, t) => await File.WriteAllBytesAsync(Path.Combine(storagePath, $"{counter++.ToString(CounterFormat)}.data"), b, t), pipe.Reader, token);
        _ = ReadImagesFromFiles(channel, storagePath, streamAfterCameraKill, token);
        return channel.Reader;
    }

    private async Task ReadImagesFromFiles(Channel<byte[]> channel, string imagePath, bool streamAfterCameraKill, CancellationToken token)
    {
        while (streamAfterCameraKill && cameraInterop.CameraIsRunning())
        {
            await Task.Delay(300, token);
        }
        if (!streamAfterCameraKill)
        {
            await StreamLive(channel, imagePath, token);
        }
        else
        {
            foreach (var file in Directory.EnumerateFiles(imagePath).OrderBy(x => x))
            {
                var bytesToSend = await File.ReadAllBytesAsync(file, token);
                await channel.Writer.WriteAsync(bytesToSend, token);
                File.Delete(file);
            }
        }
    }

    private static async Task StreamLive(Channel<byte[]> channel, string imagePath, CancellationToken token)
    {
        var counter = 0;
        while (true)
        {
            await Task.Delay(10, token);
            var currentPath = Path.Combine(imagePath, $"{counter.ToString(CounterFormat)}.data");
            if (!Path.Exists(currentPath)) continue;
            var bytesToSend = await File.ReadAllBytesAsync(currentPath, token);
            await channel.Writer.WriteAsync(bytesToSend, token);
            File.Delete(currentPath);
            counter++;
        }
    }

    public async Task<ChannelReader<byte[]>> StreamMjpeg(float resolutionDivider, int quality, float distanceInM, CancellationToken token)
    {
        var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = true,
        });
        var (pipe, _) = await cameraInterop.MjpegStream(resolutionDivider, quality, distanceInM);
        _ = WriteItemsAsync(async (b, t) => await channel.Writer.WriteAsync(b, t), pipe.Reader, token);
        return channel.Reader;
    }

    private static Func<byte, bool> BuildHeaderFinder()
    {
        var headerIndex = 0;
        return currentByte =>
        {
            if (currentByte == s_headerBytes[headerIndex]) headerIndex++;
            else headerIndex = 0;
            var headerFound = headerIndex == s_headerBytes.Length;
            if (headerFound) headerIndex = 0;
            return headerFound;
        };
    }

    private async Task WriteItemsAsync(Func<byte[], CancellationToken, Task> writeFunction, PipeReader reader, CancellationToken token)
    {
        try
        {
            var imageBuffer = new byte[1024 * 1024 * 16];
            var imageIndex = 0;
            var headerFinder = BuildHeaderFinder();
            var imageStarted = false;
            while (true)
            {
                var result = await reader.ReadAsync(token);
                foreach (var buff in result.Buffer)
                {
                    for (var i = 0; i < buff.Length; i++)
                    {
                        imageBuffer[imageIndex++] = buff.Span[i];
                        if (!headerFinder(buff.Span[i])) continue;
                        var sendBuffer = imageBuffer[0..(imageIndex - s_headerBytes.Length)];
                        imageIndex = 0;
                        if (imageStarted)
                        {
                            await writeFunction(sendBuffer, token);
                            for (var j = 0; j < s_headerBytes.Length; j++) imageBuffer[imageIndex++] = s_headerBytes[j];
                            imageStarted = false;
                            break;
                        }
                        else
                        {
                            var positionBytes = BitConverter.GetBytes(motorPosition.CurrentPosition());
                            var timestamp = BitConverter.GetBytes(DateTime.Now.ToUniversalTime().Ticks);
                            for (var j = 0; j < positionBytes.Length; j++) imageBuffer[imageIndex++] = positionBytes[j];
                            for (var j = 0; j < timestamp.Length; j++) imageBuffer[imageIndex++] = timestamp[j];
                            for (var j = 0; j < s_headerBytes.Length; j++) imageBuffer[imageIndex++] = s_headerBytes[j];
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
