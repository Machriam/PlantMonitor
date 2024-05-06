using Microsoft.AspNetCore.SignalR;
using Plantmonitor.Shared.Features.ImageStreaming;
using System.Threading.Channels;

namespace PlantMonitorControl.Features.MotorMovement;

public class StreamingHub([FromKeyedServices(ICameraInterop.VisCamera)] ICameraInterop cameraInterop,
    IFileStreamingReader fileStreamer, IMotorPositionCalculator motorPosition) : Hub
{
    public async Task<ChannelReader<byte[]>> StreamStoredMjpeg(StreamingMetaData data, CancellationToken token)
    {
        motorPosition.ResetHistory();
        var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
        {
            AllowSynchronousContinuations = false,
            FullMode = data.StoreData ? BoundedChannelFullMode.Wait : BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = true,
        });
        var folder = await cameraInterop.StreamJpgToFolder(data.ResolutionDivider, data.Quality, data.DistanceInM);
        _ = ReadImagesFromFiles(channel, folder, data, token);
        return channel.Reader;
    }

    private async Task ReadImagesFromFiles(Channel<byte[]> channel, string imagePath, StreamingMetaData data, CancellationToken token)
    {
        while (data.StoreData && cameraInterop.CameraIsRunning())
        {
            await Task.Delay(50, token);
            var steps = BitConverter.GetBytes(motorPosition.CurrentPosition());
            var tickBytes = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
            await channel.Writer.WriteAsync([.. steps, .. tickBytes], token);
        }
        if (!data.StoreData)
        {
            await StreamLive(channel, imagePath, token);
        }
        else
        {
            foreach (var file in Directory.EnumerateFiles(imagePath).OrderBy(x => x))
            {
                var fileCreationTime = File.GetCreationTimeUtc(file);
                var creationTimeBytes = BitConverter.GetBytes(fileCreationTime.Ticks);
                var stepCount = motorPosition.StepForTime(new DateTimeOffset(fileCreationTime).ToUnixTimeMilliseconds());
                var bytesOfStep = BitConverter.GetBytes(stepCount);
                var bytesToSend = await File.ReadAllBytesAsync(file, token);
                if (data.PositionsToStream.Contains(stepCount))
                {
                    await channel.Writer.WaitToWriteAsync(token);
                    await channel.Writer.WriteAsync([.. bytesOfStep, .. creationTimeBytes, .. bytesToSend], token);
                }
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
            if (!cameraInterop.CameraIsRunning()) break;
            (var creationTime, counter, var bytesToSend) = await fileStreamer.ReadNextFileWithSkipping(imagePath, counter, 10, token);
            if (bytesToSend == null) continue;
            var steps = BitConverter.GetBytes(motorPosition.CurrentPosition());
            var creationTimeBytes = BitConverter.GetBytes(creationTime.Ticks);
            await channel.Writer.WriteAsync([.. steps, .. creationTimeBytes, .. bytesToSend], token);
        }
    }
}
