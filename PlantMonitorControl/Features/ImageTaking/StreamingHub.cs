using Microsoft.AspNetCore.SignalR;
using System.Threading.Channels;

namespace PlantMonitorControl.Features.MotorMovement;

public class StreamingHub([FromKeyedServices(ICameraInterop.VisCamera)] ICameraInterop cameraInterop,
    IFileStreamingReader fileStreamer, IMotorPositionCalculator motorPosition) : Hub
{
    public async Task<ChannelReader<byte[]>> StreamStoredMjpeg(float resolutionDivider, int quality, float distanceInM,
        bool streamAfterCameraKill, CancellationToken token)
    {
        motorPosition.ResetHistory();
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
                var fileCreationTime = File.GetCreationTimeUtc(file);
                var creationTimeBytes = BitConverter.GetBytes(fileCreationTime.Ticks);
                var steps = BitConverter.GetBytes(motorPosition.StepForTime(new DateTimeOffset(fileCreationTime).ToUnixTimeMilliseconds()));
                var bytesToSend = await File.ReadAllBytesAsync(file, token);
                await channel.Writer.WaitToWriteAsync(token);
                await channel.Writer.WriteAsync([.. steps, .. creationTimeBytes, .. bytesToSend], token);
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
            (var creationTime, counter, var bytesToSend) = await fileStreamer.ReadNextFileWithSkipping(imagePath, counter, 10, token);
            var steps = BitConverter.GetBytes(motorPosition.CurrentPosition());
            var creationTimeBytes = BitConverter.GetBytes(creationTime.Ticks);
            await channel.Writer.WriteAsync([.. steps, .. creationTimeBytes, .. bytesToSend], token);
        }
    }
}
