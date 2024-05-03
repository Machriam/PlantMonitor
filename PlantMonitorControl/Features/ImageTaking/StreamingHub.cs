using Microsoft.AspNetCore.SignalR;
using System.IO.Pipelines;
using System.Threading.Channels;

namespace PlantMonitorControl.Features.MotorMovement;

public class StreamingHub([FromKeyedServices(ICameraInterop.VisCamera)] ICameraInterop cameraInterop, IMotorPositionCalculator motorPosition) : Hub
{
    private const string CounterFormat = "000000";

    public async Task<ChannelReader<byte[]>> StreamStoredMjpeg(float resolutionDivider, int quality, float distanceInM,
        string sessionId, bool streamAfterCameraKill, CancellationToken token)
    {
        var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
        {
            AllowSynchronousContinuations = false,
            FullMode = streamAfterCameraKill ? BoundedChannelFullMode.Wait : BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = true,
        });
        var folder = await cameraInterop.StreamJpgToFolder(resolutionDivider, quality, distanceInM);
        _ = ReadImagesFromFiles(channel, folder, streamAfterCameraKill, token);
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
                var creationTime = BitConverter.GetBytes(File.GetCreationTimeUtc(file).Ticks);
                var steps = BitConverter.GetBytes(9999);
                var bytesToSend = await File.ReadAllBytesAsync(file, token);
                await channel.Writer.WaitToWriteAsync(token);
                await channel.Writer.WriteAsync([.. steps, .. creationTime, .. bytesToSend], token);
                File.Delete(file);
            }
        }
    }

    private async Task StreamLive(Channel<byte[]> channel, string imagePath, CancellationToken token)
    {
        var counter = 0;
        while (true)
        {
            await Task.Delay(10, token);
            var currentPath = Path.Combine(imagePath, $"{counter.ToString(CounterFormat)}.jpg");
            var nextPath = Path.Combine(imagePath, $"{(counter + 1).ToString(CounterFormat)}.jpg");
            if (!Path.Exists(nextPath)) continue;
            var creationTime = BitConverter.GetBytes(File.GetCreationTimeUtc(currentPath).Ticks);
            var bytesToSend = await File.ReadAllBytesAsync(currentPath, token);
            var steps = BitConverter.GetBytes(motorPosition.CurrentPosition());
            await channel.Writer.WriteAsync([.. steps, .. creationTime, .. bytesToSend], token);
            File.Delete(currentPath);
            counter++;
        }
    }
}
